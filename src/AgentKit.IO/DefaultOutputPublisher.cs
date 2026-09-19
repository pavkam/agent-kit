// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

using System.Collections.Immutable;

/// <summary>The first-party <see cref="IOutputPublisher"/>: fans one run's events out to its live hub and every registered <see cref="IRunEventSink"/>.</summary>
/// <remarks>
/// <para>
/// Every accepted event is offered to the run-scoped <see cref="RunEventHub"/> (bounding live
/// <c>ReadAllAsync</c> subscribers) and then delivered, in deterministic
/// <see cref="RunEventSinkRegistration.Order"/>, to every registered sink. A
/// <see cref="RunEventDelivery.Required"/> sink's delivery is awaited inline: this method's own acceptance
/// boundary does not complete until that sink accepts the event, and an unrecovered fault or a misconfigured
/// backpressure decision (anything other than <see cref="BackpressureDecision.Wait"/>) for a required sink
/// propagates as a failure the run must observe. A <see cref="RunEventDelivery.BestEffort"/> sink's fault or
/// backpressure-driven drop is isolated: it is logged and this method continues to the next sink.
/// </para>
/// <para>
/// <see cref="CompleteAsync{TOutput}"/> seals the hub and records the single final envelope; a repeated
/// equivalent envelope is idempotent, and a conflicting one fails closed rather than silently replacing already
/// exposed evidence.
/// </para>
/// </remarks>
internal sealed class DefaultOutputPublisher: IOutputPublisher
{
    private readonly Lock _gate = new();
    private readonly ImmutableArray<IRunEventSink> _sinks;
    private readonly IOutputBackpressurePolicy _backpressurePolicy;
    private readonly RunEventHub _eventHub;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _backpressurePollInterval;
    private readonly ILogger<DefaultOutputPublisher> _logger;
    private object? _finalResult;

    /// <summary>Initializes the publisher over its run-scoped hub and the composition's registered sinks.</summary>
    /// <param name="sinks">Every sink registered for this run's composition, in registration order.</param>
    /// <param name="backpressurePolicy">The nonnull policy consulted while a sink delivery has not yet completed.</param>
    /// <param name="eventHub">The nonnull run-scoped live fan-out hub.</param>
    /// <param name="timeProvider">The nonnull injected clock used to measure how long a delivery has been blocked.</param>
    /// <param name="logger">The nonnull content-free structured logger.</param>
    /// <param name="backpressurePollInterval">How often the backpressure policy is re-consulted for a still-pending delivery.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="backpressurePollInterval"/> is not positive.</exception>
    public DefaultOutputPublisher(
        IEnumerable<IRunEventSink> sinks,
        IOutputBackpressurePolicy backpressurePolicy,
        RunEventHub eventHub,
        TimeProvider timeProvider,
        ILogger<DefaultOutputPublisher> logger,
        TimeSpan backpressurePollInterval = default)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        ArgumentNullException.ThrowIfNull(backpressurePolicy);
        ArgumentNullException.ThrowIfNull(eventHub);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        var effectivePollInterval = backpressurePollInterval == default ? TimeSpan.FromMilliseconds(100) : backpressurePollInterval;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(effectivePollInterval, TimeSpan.Zero, nameof(backpressurePollInterval));
        _sinks = [.. sinks.OrderBy(SinkOrder)];
        _backpressurePolicy = backpressurePolicy;
        _eventHub = eventHub;
        _timeProvider = timeProvider;
        _backpressurePollInterval = effectivePollInterval;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runEvent);
        _ = await _eventHub.PublishAsync(runEvent, cancellationToken).ConfigureAwait(false);
        foreach (var sink in _sinks)
        {
            if (SinkDelivery(sink) == RunEventDelivery.Required)
            {
                await DeliverRequiredAsync(sink, runEvent, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await DeliverBestEffortAsync(sink, runEvent, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask CompleteAsync<TOutput>(AgentRunFinished<TOutput> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNotEqual(result.AgentId, _eventHub.AgentId, nameof(result));
        ArgumentException.ThrowIfNotEqual(result.SessionId, _eventHub.SessionId, nameof(result));
        ArgumentException.ThrowIfNotEqual(result.ConversationId, _eventHub.ConversationId, nameof(result));
        ArgumentException.ThrowIfNotEqual(result.RunId, _eventHub.RunId, nameof(result));
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_finalResult is { } existing)
            {
                return !existing.Equals(result)
                    ? throw new InvalidOperationException("A conflicting final result was already published for this run.")
                    : ValueTask.CompletedTask;
            }

            _finalResult = result;
            _eventHub.Complete();
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Awaits one required sink's delivery, re-consulting backpressure while it remains pending.</summary>
    private async ValueTask DeliverRequiredAsync(IRunEventSink sink, RunEvent runEvent, CancellationToken cancellationToken)
    {
        var sinkName = SinkName(sink);
        var started = _timeProvider.GetTimestamp();
        var task = sink.PublishAsync(runEvent, cancellationToken);
        while (!task.IsCompleted)
        {
            var decision = await _backpressurePolicy.DecideAsync(
                RunEventDelivery.Required, _timeProvider.GetElapsedTime(started), cancellationToken).ConfigureAwait(false);
            if (decision != BackpressureDecision.Wait)
            {
                IOLog.RequiredRunEventSinkBackpressureMisconfigured(_logger, sinkName, decision);
                throw new InvalidOperationException(
                    $"The backpressure policy returned {decision} for required sink '{sinkName}'; only Wait is valid for a required sink.");
            }

            _ = await Task.WhenAny(task.AsTask(), Task.Delay(_backpressurePollInterval, _timeProvider, cancellationToken)).ConfigureAwait(false);
        }

        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            IOLog.RequiredRunEventSinkFaulted(_logger, sinkName, ErrorType(exception));
            throw;
        }
    }

    /// <summary>Delivers one best-effort sink, isolating its fault or a policy-driven drop instead of propagating either.</summary>
    private async ValueTask DeliverBestEffortAsync(IRunEventSink sink, RunEvent runEvent, CancellationToken cancellationToken)
    {
        var sinkName = SinkName(sink);
        var started = _timeProvider.GetTimestamp();
        ValueTask delivery;
        try
        {
            delivery = sink.PublishAsync(runEvent, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            IOLog.BestEffortRunEventSinkFaulted(_logger, sinkName, ErrorType(exception));
            return;
        }

        var task = delivery.AsTask();
        while (!task.IsCompleted)
        {
            var decision = await _backpressurePolicy.DecideAsync(
                RunEventDelivery.BestEffort, _timeProvider.GetElapsedTime(started), cancellationToken).ConfigureAwait(false);
            if (decision is BackpressureDecision.Drop or BackpressureDecision.Disconnect)
            {
                IOLog.BestEffortRunEventSinkBackpressured(_logger, sinkName, decision);
                return;
            }

            _ = await Task.WhenAny(task, Task.Delay(_backpressurePollInterval, _timeProvider, cancellationToken)).ConfigureAwait(false);
        }

        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            IOLog.BestEffortRunEventSinkFaulted(_logger, sinkName, ErrorType(exception));
        }
    }

    private static RunEventDelivery SinkDelivery(IRunEventSink sink) =>
        sink is RunEventSinkBinding binding ? binding.Registration.Delivery : RunEventDelivery.BestEffort;

    private static int SinkOrder(IRunEventSink sink) =>
        sink is RunEventSinkBinding binding ? binding.Registration.Order : 0;

    private static string SinkName(IRunEventSink sink) =>
        sink is RunEventSinkBinding binding ? binding.Registration.SinkName : sink.GetType().Name;

    private static string ErrorType(Exception exception) => exception.GetType().FullName ?? exception.GetType().Name;
}
