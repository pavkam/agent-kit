// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Publishes ordered live events and exactly one final envelope for a single already accepted run.</summary>
/// <remarks>
/// This first-party publisher owns run correlation, strict local sequence advancement, bounded live fan-out, and single-winner final-result exposure.
/// It deliberately owns no durable behavior: it reserves no durable sequence range, records no publication intent, delivers to no required external sink,
/// and bounds no event payload bytes. It also never decides a semantic outcome; the envelope it exposes is already settled or already requires recovery.
/// </remarks>
public sealed class AgentRunOutputPublisher: IOutputPublisher, IAsyncDisposable
{
    private readonly Lock _gate = new();
    private readonly TaskCompletionSource<object> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly RunEventHub _hub;
    private readonly AgentId _agentId;
    private readonly SessionId _sessionId;
    private readonly ConversationId? _conversationId;
    private readonly RunId _runId;
    private readonly ILogger _logger;
    private Type? _outputType;
    private object? _finalResult;

    /// <summary>Creates an empty run-owned publisher with immutable correlation and explicit live bounds.</summary>
    /// <param name="agentId">The nondefault accepted agent identity.</param>
    /// <param name="sessionId">The nondefault session identity.</param>
    /// <param name="conversationId">The optional nondefault conversation identity.</param>
    /// <param name="runId">The nondefault accepted run identity.</param>
    /// <param name="timeProvider">The nonnull injected observational clock.</param>
    /// <param name="options">The live fan-out bounds, or <see langword="null"/> to select this package's documented defaults.</param>
    /// <param name="logger">An optional content-free structured logger; <see langword="null"/> selects a no-op logger.</param>
    /// <exception cref="ArgumentOutOfRangeException">A required identity is default or a supplied optional identity is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is null.</exception>
    public AgentRunOutputPublisher(
        AgentId agentId,
        SessionId sessionId,
        ConversationId? conversationId,
        RunId runId,
        TimeProvider timeProvider,
        RunOutputPublisherOptions? options = null,
        ILogger<AgentRunOutputPublisher>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _hub = new RunEventHub(
            agentId,
            sessionId,
            conversationId,
            runId,
            (options ?? new RunOutputPublisherOptions()).ToHubOptions(),
            timeProvider);
        _agentId = agentId;
        _sessionId = sessionId;
        _conversationId = conversationId;
        _runId = runId;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentRunOutputPublisher>.Instance;
    }

    /// <summary>Registers one bounded live subscriber whose final-result wait is independent of event delivery.</summary>
    /// <typeparam name="TOutput">The run's validated output snapshot type.</typeparam>
    /// <returns>A caller-owned stream that receives only subsequent publications and the single final envelope.</returns>
    /// <remarks>The first subscription or completion binds this run's output type. Abandoning the returned stream does not cancel the run.</remarks>
    /// <exception cref="InvalidOperationException">This run is already bound to a different output type.</exception>
    /// <exception cref="RunEventSubscriptionRejectedException">Publication already ended or the simultaneous subscriber bound was reached.</exception>
    public IAgentRunStream<TOutput> Subscribe<TOutput>()
    {
        BindOutputType<TOutput>();
        return _hub.Subscribe(ProjectAsync<TOutput>(_completion.Task));
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Publication already ended because the final envelope was exposed or the publisher was disposed.</exception>
    public async ValueTask PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runEvent);
        var outcome = await _hub.PublishAsync(runEvent, cancellationToken).ConfigureAwait(false);
        switch (outcome)
        {
            case RunEventPublicationOutcome.Published:
                return;
            case RunEventPublicationOutcome.OutOfOrder:
                throw new ArgumentException(
                    "The event sequence does not advance strictly beyond this run's last accepted event.",
                    nameof(runEvent));
            case RunEventPublicationOutcome.HubClosed:
            default:
                throw new InvalidOperationException(
                    "This run's event publication has ended and cannot accept another event.");
        }
    }

    /// <inheritdoc/>
    public ValueTask CompleteAsync<TOutput>(AgentRunFinished<TOutput> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNotEqual(result.AgentId, _agentId, nameof(result));
        ArgumentException.ThrowIfNotEqual(result.SessionId, _sessionId, nameof(result));
        ArgumentException.ThrowIfNotEqual(result.ConversationId, _conversationId, nameof(result));
        ArgumentException.ThrowIfNotEqual(result.RunId, _runId, nameof(result));
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_outputType is { } bound && bound != typeof(TOutput))
            {
                throw new ArgumentException(
                    "This run is already bound to a different validated output type.",
                    nameof(result));
            }
            if (_finalResult is { } existing)
            {
                if (!existing.Equals(result))
                {
                    SafeLogConflict();
                    throw new InvalidOperationException(
                        "A conflicting final result was already published for this run.");
                }
                return ValueTask.CompletedTask;
            }
            _outputType = typeof(TOutput);
            _finalResult = result;

            // Sealing the event stream and exposing the envelope happen inside the same critical section that
            // recorded _finalResult, so a concurrent DisposeAsync can never observe _finalResult set without
            // _completion already carrying that same result: a settlement that already happened is never
            // replaced by a disposal-triggered cancellation.
            _hub.Complete();
            _ = _completion.TrySetResult(result);
        }

        SafeLogCompleted(result.Outcome.GetType().Name);
        return ValueTask.CompletedTask;
    }

    /// <summary>Ends live delivery and releases every pending recipient buffer.</summary>
    /// <returns>A synchronously completed disposal; repeated disposal is harmless.</returns>
    /// <remarks>Disposal before a final envelope was exposed cancels every pending final-result wait rather than fabricating an outcome. It never changes a settlement that already happened.</remarks>
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_finalResult is null)
            {
                _ = _completion.TrySetCanceled();
            }
        }

        return _hub.DisposeAsync();
    }

    /// <summary>Binds this run's single validated output type before a typed wait is created.</summary>
    /// <typeparam name="TOutput">The requested output snapshot type.</typeparam>
    /// <exception cref="InvalidOperationException">Another output type is already bound to this run.</exception>
    private void BindOutputType<TOutput>()
    {
        lock (_gate)
        {
            if (_outputType is { } bound && bound != typeof(TOutput))
            {
                throw new InvalidOperationException(
                    "This run is already bound to a different validated output type.");
            }
            _outputType = typeof(TOutput);
        }
    }

    /// <summary>Projects the type-bound completion onto the caller's typed final-result wait.</summary>
    /// <typeparam name="TOutput">The bound output snapshot type.</typeparam>
    /// <param name="completion">The publisher-owned completion source task.</param>
    /// <returns>The typed envelope once exposed; cancellation propagates unchanged.</returns>
    /// <exception cref="InvalidOperationException">The exposed envelope carries another output type; output-type binding makes this unreachable.</exception>
    private static async Task<AgentRunFinished<TOutput>> ProjectAsync<TOutput>(Task<object> completion)
    {
        var exposed = await completion.ConfigureAwait(false);
        return exposed is AgentRunFinished<TOutput> finished
            ? finished
            : throw new InvalidOperationException("This run exposed a final envelope of another validated output type.");
    }

    /// <summary>Records content-free final-result evidence without changing exposure.</summary>
    /// <param name="outcome">The stable outcome type name.</param>
    private void SafeLogCompleted(string outcome)
    {
        try
        {
            IOLog.FinalResultPublished(_logger, outcome);
        }
        catch
        {
            // Logging is observational and cannot change the exposed final envelope.
        }
    }

    /// <summary>Records a rejected conflicting final result without changing the retained envelope.</summary>
    private void SafeLogConflict()
    {
        try
        {
            IOLog.FinalResultConflicted(_logger);
        }
        catch
        {
            // Logging is observational and cannot change the retained final envelope.
        }
    }
}
