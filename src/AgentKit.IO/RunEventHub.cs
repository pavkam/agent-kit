// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Owns ordered, bounded process-local fan-out for one already accepted run.</summary>
/// <remarks>The publisher owns sequence allocation, durable event acceptance, payload-byte bounds, authorization and final settlement. This hub receives immutable presequenced events only. Saturated recipients terminate explicitly; they never block the producer or silently lose a durable event.</remarks>
internal sealed class RunEventHub: IAsyncDisposable
{
    private readonly Lock _gate = new();
    private readonly HashSet<RunEventSubscription> _subscriptions = [];
    private readonly RunEventHubOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RunEventHub> _logger;
    private long _lastSequence;
    private bool _closed;

    /// <summary>Creates an empty run-owned hub with explicit immutable bounds and correlation.</summary>
    /// <param name="agentId">The nondefault accepted agent identity.</param>
    /// <param name="sessionId">The nondefault session identity.</param>
    /// <param name="conversationId">The optional nondefault conversation identity.</param>
    /// <param name="runId">The nondefault accepted run identity.</param>
    /// <param name="options">The nonnull validated subscriber and queue bounds.</param>
    /// <param name="timeProvider">The nonnull injected observational clock.</param>
    /// <param name="logger">An optional hub logger; null selects a no-op logger.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> or <paramref name="timeProvider"/> is null.</exception>
    internal RunEventHub(AgentId agentId, SessionId sessionId, ConversationId? conversationId, RunId runId, RunEventHubOptions options, TimeProvider timeProvider, ILogger<RunEventHub>? logger = null)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        if (conversationId is { } conversation) { ArgumentOutOfRangeException.ThrowIfEqual(conversation, default, nameof(conversationId)); }
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        AgentId = agentId;
        SessionId = sessionId;
        ConversationId = conversationId;
        RunId = runId;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RunEventHub>.Instance;
    }

    /// <summary>Gets the accepted agent identity this hub's events must carry.</summary>
    internal AgentId AgentId { get; }

    /// <summary>Gets the session identity this hub's events must carry.</summary>
    internal SessionId SessionId { get; }

    /// <summary>Gets the optional conversation identity this hub's events must carry.</summary>
    internal ConversationId? ConversationId { get; }

    /// <summary>Gets the accepted run identity this hub's events must carry.</summary>
    internal RunId RunId { get; }

    /// <summary>Atomically registers one empty bounded buffer before the consumer starts reading.</summary>
    /// <returns>A non-owning single-reader subscription receiving only subsequent publications.</returns>
    /// <exception cref="RunEventSubscriptionRejectedException">The hub closed or the simultaneous subscriber bound was reached.</exception>
    internal RunEventSubscription Subscribe()
    {
        using var observation = Observe(RunEventHubOperation.Subscribe);
        lock (_gate)
        {
            if (_closed) { observation.Finish(RunEventHubOutcome.HubClosed); throw new RunEventSubscriptionRejectedException(RunEventSubscriptionRejection.HubClosed); }
            if (_subscriptions.Count == _options.MaximumSubscriptions)
            {
                observation.Finish(RunEventHubOutcome.CapacityReached);
                throw new RunEventSubscriptionRejectedException(RunEventSubscriptionRejection.CapacityReached);
            }
            var subscription = new RunEventSubscription(_options.CapacityPerSubscription, Remove, Observe);
            _ = _subscriptions.Add(subscription);
            observation.Finish(RunEventHubOutcome.Succeeded);
            return subscription;
        }
    }

    /// <summary>Registers a typed non-owning stream with final-result waiting independent of event delivery.</summary>
    /// <typeparam name="TOutput">The run's validated output snapshot type.</typeparam>
    /// <param name="completion">The nonnull producer-owned task completed after the bounded settlement attempt.</param>
    /// <returns>A caller-owned subscription whose event cancellation and disposal do not cancel final-result waiting.</returns>
    /// <exception cref="ArgumentNullException">The completion task is null.</exception>
    /// <exception cref="RunEventSubscriptionRejectedException">The hub is closed or its subscription bound is reached.</exception>
    internal IAgentRunStream<TOutput> Subscribe<TOutput>(Task<AgentRunFinished<TOutput>> completion)
    {
        ArgumentNullException.ThrowIfNull(completion);
        return new RunEventStream<TOutput>(Subscribe(), AgentId, SessionId, ConversationId, RunId, completion, Observe);
    }

    /// <summary>Captures recipients and offers one event atomically with the run-local sequence check.</summary>
    /// <param name="runEvent">The nonnull immutable event with exactly this hub's run correlation.</param>
    /// <param name="cancellationToken">Cancels before acceptance; after acceptance all captured recipients are offered the event synchronously.</param>
    /// <returns>A typed outcome; rejection leaves both sequence and recipient buffers unchanged. Sequence gaps remain the publisher's loss/resnapshot responsibility.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="runEvent"/> is null.</exception>
    /// <exception cref="ArgumentException">The event addresses a different agent, session, conversation or run.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before acceptance.</exception>
    internal ValueTask<RunEventPublicationOutcome> PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runEvent);
        ArgumentException.ThrowIfNotEqual(runEvent.AgentId, AgentId, nameof(runEvent));
        ArgumentException.ThrowIfNotEqual(runEvent.SessionId, SessionId, nameof(runEvent));
        ArgumentException.ThrowIfNotEqual(runEvent.ConversationId, ConversationId, nameof(runEvent));
        ArgumentException.ThrowIfNotEqual(runEvent.RunId, RunId, nameof(runEvent));
        using var observation = Observe(RunEventHubOperation.Publish);
        var disconnected = 0;
        try
        {
            lock (_gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_closed)
                {
                    observation.Finish(RunEventHubOutcome.HubClosed);
                    return ValueTask.FromResult(RunEventPublicationOutcome.HubClosed);
                }
                if (runEvent.Sequence <= _lastSequence)
                {
                    observation.Finish(RunEventHubOutcome.OutOfOrder);
                    return ValueTask.FromResult(RunEventPublicationOutcome.OutOfOrder);
                }
                _lastSequence = runEvent.Sequence;
                foreach (var subscription in _subscriptions.ToArray())
                {
                    if (!subscription.Offer(runEvent))
                    {
                        if (subscription.State == RunEventSubscriptionState.SlowConsumer) { disconnected++; }
                        _ = _subscriptions.Remove(subscription);
                    }
                }
            }
            for (var index = 0; index < disconnected; index++)
            {
                using var disconnection = Observe(RunEventHubOperation.Disconnect);
                disconnection.Finish(RunEventHubOutcome.SlowConsumer);
            }
            observation.Finish(RunEventHubOutcome.Succeeded);
            return ValueTask.FromResult(RunEventPublicationOutcome.Published);
        }
        catch (OperationCanceledException)
        {
            observation.Finish(RunEventHubOutcome.Cancelled);
            throw;
        }
    }

    /// <summary>Seals the recipient set and permits each existing subscriber to drain its accepted prefix.</summary>
    /// <remarks>This is event-stream completion only, never proof of run settlement or external sink delivery. The consumer owns any remaining bounded queue until it drains or disposes it.</remarks>
    internal void Complete()
    {
        using var observation = Observe(RunEventHubOperation.Complete);
        observation.Finish(RunEventHubOutcome.Succeeded);
        lock (_gate)
        {
            if (_closed) { return; }
            _closed = true;
            foreach (var subscription in _subscriptions) { subscription.End(completed: true); }
            _subscriptions.Clear();
        }
    }

    /// <summary>Terminates active subscriptions with explicit delivery failure and releases pending buffers.</summary>
    /// <returns>A synchronously completed disposal. Completed streams retain their consumer-owned drain buffers; repeated disposal is harmless.</returns>
    public ValueTask DisposeAsync()
    {
        using var observation = Observe(RunEventHubOperation.Dispose);
        observation.Finish(RunEventHubOutcome.Succeeded);
        lock (_gate)
        {
            if (!_closed)
            {
                _closed = true;
                foreach (var subscription in _subscriptions) { subscription.End(completed: false); }
                _subscriptions.Clear();
            }
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>Releases a recipient registration after its own queue lock has been released.</summary>
    /// <param name="subscription">The nonnull subscriber owned by this hub.</param>
    private void Remove(RunEventSubscription subscription)
    {
        Debug.Assert(subscription is not null, "Only this hub's initialized subscribers can release a registration.");
        lock (_gate) { _ = _subscriptions.Remove(subscription); }
    }
    /// <summary>Starts observation using only validated immutable run correlation and the injected clock.</summary>
    /// <param name="operation">The caller-established bounded operation kind.</param>
    /// <returns>A single-owner scope whose observer failures cannot change delivery.</returns>
    private RunEventHubObservation Observe(RunEventHubOperation operation)
    {
        Debug.Assert(Enum.IsDefined(operation), "Only package-defined hub operations are observed.");
        return new RunEventHubObservation(operation, AgentId, SessionId, RunId, _timeProvider, _logger);
    }
}
