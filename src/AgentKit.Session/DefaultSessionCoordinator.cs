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
/// a sink failure is not currently surfaced to the caller of this reduced
/// coordinator (best-effort delivery), since required durable delivery
/// belongs to the not-yet-implemented observability integration.
/// </remarks>
internal sealed class DefaultSessionCoordinator: ISessionCoordinator
{
    private readonly ISessionStore _store;
    private readonly ImmutableArray<ISessionEventSink> _eventSinks;
    private readonly TimeProvider _timeProvider;
    private readonly AgentSessionOptions _options;

    /// <summary>Initializes a new instance of the <see cref="DefaultSessionCoordinator"/> class.</summary>
    /// <param name="store">The single registered session store this coordinator delegates to.</param>
    /// <param name="eventSinks">The additive, ordered set of registered event sinks.</param>
    /// <param name="timeProvider">The clock used to timestamp published events.</param>
    /// <param name="options">The validated session coordination options.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public DefaultSessionCoordinator(
        ISessionStore store,
        IEnumerable<ISessionEventSink> eventSinks,
        TimeProvider timeProvider,
        IOptions<AgentSessionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(eventSinks);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);

        _store = store;
        _eventSinks = [.. eventSinks];
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public async ValueTask<SessionCreateResult> CreateAsync(
        SessionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _store.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        if (result is SessionCreated created)
        {
            await PublishAsync(
                new SessionCreatedEvent(created.Descriptor.Address, _timeProvider.GetUtcNow(), created.Descriptor),
                cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <inheritdoc/>
    public ValueTask<SessionLoadResult> LoadAsync(
        SessionOperationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _store.LoadAsync(context, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<SessionAppendResult> AppendAsync(
        SessionAppendRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Entries.Length > _options.MaximumAppendEntries)
        {
            return new SessionAppendFailed(
                $"Append request carries {request.Entries.Length} entries, exceeding the configured maximum of {_options.MaximumAppendEntries}.");
        }

        var result = await _store.AppendAsync(request, cancellationToken).ConfigureAwait(false);
        if (result is SessionAppended appended)
        {
            await PublishAsync(
                new SessionAppendedEvent(
                    request.Context.ToAddress(),
                    _timeProvider.GetUtcNow(),
                    request.BranchId,
                    appended.NewVersion,
                    appended.CommittedEntries.Length),
                cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <inheritdoc/>
    public ValueTask<SessionPageResult> ReadAsync(
        SessionReadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.PageSize > _options.MaximumPageSize
            ? ValueTask.FromResult<SessionPageResult>(new SessionReadFailed(
                $"Requested page size {request.PageSize} exceeds the configured maximum of {_options.MaximumPageSize}."))
            : _store.ReadAsync(request, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<SessionBranchResult> BranchAsync(
        SessionBranchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _store.CreateBranchAsync(request, cancellationToken).ConfigureAwait(false);
        if (result is SessionBranched branched)
        {
            await PublishAsync(
                new SessionBranchedEvent(
                    request.Context.ToAddress(),
                    _timeProvider.GetUtcNow(),
                    request.ParentBranchId,
                    branched.NewBranchId,
                    branched.ForkedAtSequence),
                cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <inheritdoc/>
    public async ValueTask<SessionDeleteResult> DeleteAsync(
        SessionDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _store.DeleteAsync(request, cancellationToken).ConfigureAwait(false);
        if (result is SessionDeleted)
        {
            await PublishAsync(
                new SessionDeletedEvent(request.Context.ToAddress(), _timeProvider.GetUtcNow()),
                cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private async ValueTask PublishAsync(SessionEvent sessionEvent, CancellationToken cancellationToken)
    {
        foreach (var sink in _eventSinks)
        {
            await sink.PublishAsync(sessionEvent, cancellationToken).ConfigureAwait(false);
        }
    }
}
