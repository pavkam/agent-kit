// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

using Microsoft.Extensions.Options;

/// <summary>
/// The default <see cref="ISessionCoordinator"/>: enforces the configured
/// request-size ceilings, delegates every operation to the single
/// registered <see cref="ISessionStore"/>, and publishes a semantic event
/// for every operation that mutated durable state.
/// </summary>
/// <remarks>
/// This class never implements storage itself and never invokes the agent
/// loop, input coordinator, or context assembler. Publishing to
/// <see cref="ISessionEventSink"/> instances happens after the store call
/// returns a successful outcome and cannot influence or veto that outcome;
/// sink delivery is best-effort and settlement-safe: after a successful store
/// mutation, each sink is attempted independently, and sink failure or caller
/// cancellation cannot replace the already-committed result. Required durable
/// delivery belongs to the not-yet-implemented observability integration.
/// </remarks>
internal sealed class DefaultSessionCoordinator: ISessionCoordinator
{
    private readonly ISessionStore _store;
    private readonly ImmutableArray<ISessionEventSink> _eventSinks;
    private readonly TimeProvider _timeProvider;
    private readonly AgentSessionOptions _options;
    private readonly ILogger<DefaultSessionCoordinator> _logger;

    /// <summary>Initializes a new instance of the <see cref="DefaultSessionCoordinator"/> class.</summary>
    /// <param name="store">The single registered session store this coordinator delegates to.</param>
    /// <param name="eventSinks">The additive, ordered set of registered event sinks.</param>
    /// <param name="timeProvider">The clock used to timestamp published events.</param>
    /// <param name="options">The validated session coordination options.</param>
    /// <param name="logger">
    /// The optional logger that receives safe session diagnostics; a Microsoft
    /// null logger is used when omitted.
    /// </param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public DefaultSessionCoordinator(
        ISessionStore store,
        IEnumerable<ISessionEventSink> eventSinks,
        TimeProvider timeProvider,
        IOptions<AgentSessionOptions> options,
        ILogger<DefaultSessionCoordinator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(eventSinks);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);

        _store = store;
        _eventSinks = [.. eventSinks];
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultSessionCoordinator>.Instance;
    }

    /// <inheritdoc/>
    public async ValueTask<SessionCreateResult> CreateAsync(
        SessionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await ObserveAsync(
            AgentKitActivityNames.SessionCreate,
            request.AgentId,
            sessionId: null,
            operationId: null,
            async () =>
            {
                var result = await _store.CreateAsync(request, cancellationToken).ConfigureAwait(false);
                if (result is SessionCreated created)
                {
                    await PublishBestEffortAsync(
                        new SessionCreatedEvent(created.Descriptor.Address, _timeProvider.GetUtcNow(), created.Descriptor),
                        cancellationToken).ConfigureAwait(false);
                }

                return result;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<SessionLoadResult> LoadAsync(
        SessionOperationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return await ObserveAsync(
            AgentKitActivityNames.SessionLoad,
            context.AgentId,
            context.SessionId,
            context.Correlation.OperationId,
            () => _store.LoadAsync(context, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<SessionAppendResult> AppendAsync(
        SessionAppendRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await ObserveAsync(
            AgentKitActivityNames.SessionCommit,
            request.Context.AgentId,
            request.Context.SessionId,
            request.Context.Correlation.OperationId,
            async () =>
            {
                if (request.Entries.Length > _options.MaximumAppendEntries)
                {
                    return new SessionAppendFailed(
                        $"Append request carries {request.Entries.Length} entries, exceeding the configured maximum of {_options.MaximumAppendEntries}.");
                }

                var result = await _store.AppendAsync(request, cancellationToken).ConfigureAwait(false);
                if (result is SessionAppended appended)
                {
                    await PublishBestEffortAsync(
                        new SessionAppendedEvent(
                            request.Context.ToAddress(),
                            _timeProvider.GetUtcNow(),
                            request.BranchId,
                            appended.NewVersion,
                            appended.CommittedEntries.Length),
                        cancellationToken).ConfigureAwait(false);
                }

                return result;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<SessionPageResult> ReadAsync(
        SessionReadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await ObserveAsync(
            AgentKitActivityNames.SessionRead,
            request.Context.AgentId,
            request.Context.SessionId,
            request.Context.Correlation.OperationId,
            () => request.PageSize > _options.MaximumPageSize
                ? ValueTask.FromResult<SessionPageResult>(new SessionReadFailed(
                    $"Requested page size {request.PageSize} exceeds the configured maximum of {_options.MaximumPageSize}."))
                : _store.ReadAsync(request, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<SessionBranchResult> BranchAsync(
        SessionBranchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await ObserveAsync(
            AgentKitActivityNames.SessionBranch,
            request.Context.AgentId,
            request.Context.SessionId,
            request.Context.Correlation.OperationId,
            async () =>
            {
                var result = await _store.CreateBranchAsync(request, cancellationToken).ConfigureAwait(false);
                if (result is SessionBranched branched)
                {
                    await PublishBestEffortAsync(
                        new SessionBranchedEvent(
                            request.Context.ToAddress(),
                            _timeProvider.GetUtcNow(),
                            request.ParentBranchId,
                            branched.NewBranchId,
                            branched.ForkedAtSequence),
                        cancellationToken).ConfigureAwait(false);
                }

                return result;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<SessionDeleteResult> DeleteAsync(
        SessionDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await ObserveAsync(
            AgentKitActivityNames.SessionDelete,
            request.Context.AgentId,
            request.Context.SessionId,
            request.Context.Correlation.OperationId,
            async () =>
            {
                var result = await _store.DeleteAsync(request, cancellationToken).ConfigureAwait(false);
                if (result is SessionDeleted)
                {
                    await PublishBestEffortAsync(
                        new SessionDeletedEvent(request.Context.ToAddress(), _timeProvider.GetUtcNow()),
                        cancellationToken).ConfigureAwait(false);
                }

                return result;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<TResult> ObserveAsync<TResult>(
        string operation,
        AgentId agentId,
        SessionId? sessionId,
        OperationId? operationId,
        Func<ValueTask<TResult>> action,
        CancellationToken cancellationToken)
        where TResult : class
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "A stable session operation name is required.");
        Debug.Assert(action is not null, "A session operation delegate is required.");
        using var activity = AgentKitDiagnostics.Activities.StartActivity(
            operation,
            ActivityKind.Internal,
            parentContext: Activity.Current?.Context ?? default,
            tags: new ActivityTagsCollection
            {
                { AgentKitTagNames.GenAiOperationName, operation },
                { AgentKitTagNames.AgentId, agentId.ToString() },
                { AgentKitTagNames.SessionId, sessionId?.ToString() },
                { AgentKitTagNames.OperationId, operationId?.ToString() },
                { AgentKitTagNames.SessionOperation, operation },
            });
        SessionLog.OperationStarted(_logger, operation, agentId, sessionId);

        try
        {
            var result = await action().ConfigureAwait(false);
            var outcome = result.GetType().Name;
            if (result is SessionCreated or SessionLoaded or SessionAppended or SessionPage or SessionBranched or SessionDeleted)
            {
                activity.SetSuccessful(outcome);
            }
            else
            {
                activity.SetFailed(outcome, outcome);
            }

            SessionMetrics.Operations.Add(
                1,
                new KeyValuePair<string, object?>(AgentKitTagNames.SessionOperation, operation),
                new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
            SessionLog.OperationCompleted(_logger, operation, agentId, sessionId, outcome);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", "cancellation");
            SessionMetrics.Operations.Add(
                1,
                new KeyValuePair<string, object?>(AgentKitTagNames.SessionOperation, operation),
                new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, "cancelled"));
            SessionLog.OperationCancelled(_logger, operation, agentId, sessionId);
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            activity.SetFailed("faulted", errorType);
            SessionMetrics.Operations.Add(
                1,
                new KeyValuePair<string, object?>(AgentKitTagNames.SessionOperation, operation),
                new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, "faulted"));
            SessionLog.OperationFaulted(
                _logger,
                operation,
                agentId,
                sessionId,
                exception.GetType().FullName ?? exception.GetType().Name);
            throw;
        }
    }

    private async ValueTask PublishBestEffortAsync(
        SessionEvent sessionEvent,
        CancellationToken cancellationToken)
    {
        foreach (var sink in _eventSinks)
        {
            var sinkName = sink.GetType().FullName ?? sink.GetType().Name;
            using var activity = AgentKitDiagnostics.Activities.StartActivity(
                AgentKitActivityNames.SessionEventPublish,
                ActivityKind.Internal,
                parentContext: Activity.Current?.Context ?? default,
                tags: new ActivityTagsCollection
                {
                    { AgentKitTagNames.GenAiOperationName, AgentKitActivityNames.SessionEventPublish },
                    { AgentKitTagNames.AgentId, sessionEvent.Address.AgentId.ToString() },
                    { AgentKitTagNames.SessionId, sessionEvent.Address.SessionId.ToString() },
                    { AgentKitTagNames.ObserverName, sinkName },
                });
            try
            {
                await sink.PublishAsync(sessionEvent, cancellationToken).ConfigureAwait(false);
                activity.SetSuccessful("published");
            }
            catch (Exception exception)
            {
                var errorType = exception.GetType().FullName ?? exception.GetType().Name;
                activity.SetFailed("failed", errorType);
                SessionLog.EventSinkFailed(
                    _logger,
                    sinkName,
                    sessionEvent.Address.SessionId,
                    exception.GetType().FullName ?? exception.GetType().Name);
                // Durable state has already committed. Best-effort observation
                // cannot change or obscure that result, and one failed sink
                // cannot prevent later sinks from receiving the same event.
            }
        }
    }
}
