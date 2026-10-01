// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using Microsoft.Extensions.Options;

/// <summary>Delivers immutable tool events to every registered sink in registration order and isolates every sink failure.</summary>
/// <remarks>
/// <para>
/// Delivery is strictly observational. A sink that throws, faults, or exceeds
/// <see cref="ToolRuntimeOptions.EventSinkTimeout"/> is counted and logged by identity and exception type only; it never
/// changes a result, a recording, a retry decision, or another sink's delivery. A sink that ignores its cancellation
/// token is abandoned after the timeout rather than awaited forever. Cancellation by the caller always propagates, so a
/// batch that is being cancelled does not wait on observers.
/// </para>
/// <para>The dispatcher is immutable after construction and safe for concurrent use. It owns no sink.</para>
/// </remarks>
public sealed class ToolEventDispatcher
{
    private readonly ImmutableArray<ToolEventSinkBinding> _bindings;
    private readonly TimeSpan _timeout;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ToolEventDispatcher> _logger;

    /// <summary>Initializes the dispatcher over the registered sinks.</summary>
    /// <param name="bindings">Every registered sink; ordered by registration order then ordinal identity.</param>
    /// <param name="options">The runtime options that supply the per-sink delivery timeout.</param>
    /// <param name="timeProvider">The replaceable clock that bounds each delivery.</param>
    /// <param name="logger">The type-specific structured logger.</param>
    /// <exception cref="ArgumentNullException">A dependency or binding is null.</exception>
    public ToolEventDispatcher(
        IEnumerable<ToolEventSinkBinding> bindings,
        IOptions<ToolRuntimeOptions> options,
        TimeProvider timeProvider,
        ILogger<ToolEventDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _bindings = [.. bindings
            .Select(static binding => binding ?? throw new ArgumentNullException(nameof(bindings)))
            .OrderBy(static binding => binding.Registration.Order)
            .ThenBy(static binding => binding.Registration.Id.Value, StringComparer.Ordinal)];
        _timeout = options.Value.EventSinkTimeout;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Delivers one event to every sink in order.</summary>
    /// <param name="toolEvent">The nonnull immutable event.</param>
    /// <param name="cancellationToken">Cancels delivery; cancellation propagates to the caller and is never counted as a sink failure.</param>
    /// <returns>A task that completes after every sink observed, failed, or timed out.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="toolEvent"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public async ValueTask PublishAsync(ToolEvent toolEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(toolEvent);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var binding in _bindings)
        {
            await DeliverAsync(binding, toolEvent, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task DeliverAsync(ToolEventSinkBinding binding, ToolEvent toolEvent, CancellationToken cancellationToken)
    {
        var sinkId = binding.Registration.Id.Value;
        using var timeout = new CancellationTokenSource(_timeout, _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            var delivery = binding.Sink.PublishAsync(toolEvent, linked.Token).AsTask();
            _ = delivery.ContinueWith(
                static task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            await delivery.WaitAsync(_timeout, _timeProvider, cancellationToken).ConfigureAwait(false);
            Observe(() => ToolRecordingMetrics.EventPublishCount.Add(1, Outcome("delivered")));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception is TimeoutException or OperationCanceledException ? "Timeout" : exception.GetType().Name;
            Observe(() => ToolLog.EventSinkFailed(_logger, sinkId, errorType));
            Observe(() => ToolRecordingMetrics.EventPublishCount.Add(1, Outcome("failed")));
        }
    }

    private static TagList Outcome(string outcome) => new() { { AgentKitTagNames.Outcome, outcome } };

    private static void Observe(Action observation)
    {
        Debug.Assert(observation is not null, "The dispatcher supplies each observation callback.");
        try
        {
            observation();
        }
        catch
        {
            // Instrumentation is observational only and cannot change delivery.
        }
    }
}
