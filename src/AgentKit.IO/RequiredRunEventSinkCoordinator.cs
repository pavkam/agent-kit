// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

using System.Collections.Immutable;
using System.Diagnostics;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Drains required run-event sinks at engine shutdown, each within the flush deadline its registration declares.</summary>
/// <remarks>
/// <para>
/// A required sink's <see cref="IRunEventSink.PublishAsync"/> is awaited inline for every event, so a sink with no internal
/// buffer has nothing left to drain. A sink that buffers implements <see cref="IFlushableRunEventSink"/>; this coordinator
/// starts every such flush before awaiting any of them, then waits at most each sink's own
/// <see cref="RunEventSinkRegistration.FlushDeadline"/>, measured on the injected <see cref="TimeProvider"/>. A deadline,
/// a faulted flush, or a sink that ignores cancellation is reported in the result rather than thrown, so shutdown stays
/// bounded and a failed sink cannot hide the others.
/// </para>
/// <para>Only the caller's cancellation propagates. It stops waiting and does not rewrite an accepted event as persisted.</para>
/// </remarks>
internal sealed class RequiredRunEventSinkCoordinator: IRequiredRunEventSinkCoordinator
{
    private readonly ImmutableArray<RunEventSinkBinding> _required;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    /// <summary>Initializes the coordinator over the composition's registered sinks.</summary>
    /// <param name="sinks">Every registered sink; only <see cref="RunEventDelivery.Required"/> bindings are drained.</param>
    /// <param name="timeProvider">The injected clock that measures each flush deadline.</param>
    /// <param name="logger">The content-free structured logger, or <see langword="null"/> to disable drain logging.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sinks"/> or <paramref name="timeProvider"/> is null.</exception>
    public RequiredRunEventSinkCoordinator(
        IEnumerable<IRunEventSink> sinks,
        TimeProvider timeProvider,
        ILogger<RequiredRunEventSinkCoordinator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _required =
        [
            .. sinks
                .OfType<RunEventSinkBinding>()
                .Where(static binding => binding.Registration.Delivery == RunEventDelivery.Required)
                .OrderBy(static binding => binding.Registration.Order),
        ];
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger<RequiredRunEventSinkCoordinator>.Instance;
    }

    /// <inheritdoc/>
    public async ValueTask<RequiredRunEventSinkDrainResult> DrainAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_required.IsEmpty)
        {
            return RequiredRunEventSinkDrainResult.Empty;
        }

        var drains = new Task<DrainOutcome>[_required.Length];
        for (var index = 0; index < _required.Length; index++)
        {
            drains[index] = DrainOneAsync(_required[index], cancellationToken);
        }

        var outcomes = await Task.WhenAll(drains).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var drained = ImmutableArray.CreateBuilder<string>();
        var timedOut = ImmutableArray.CreateBuilder<string>();
        var failed = ImmutableArray.CreateBuilder<string>();
        for (var index = 0; index < outcomes.Length; index++)
        {
            var name = _required[index].Registration.SinkName;
            switch (outcomes[index])
            {
                case DrainOutcome.TimedOut:
                    timedOut.Add(name);
                    break;
                case DrainOutcome.Failed:
                    failed.Add(name);
                    break;
                case DrainOutcome.Drained:
                    drained.Add(name);
                    break;
                default:
                    throw new UnreachableException();
            }
        }

        return new RequiredRunEventSinkDrainResult(drained.ToImmutable(), timedOut.ToImmutable(), failed.ToImmutable());
    }

    private async Task<DrainOutcome> DrainOneAsync(RunEventSinkBinding binding, CancellationToken cancellationToken)
    {
        var name = binding.Registration.SinkName;
        if (binding.Inner is not IFlushableRunEventSink flushable)
        {
            SafeLog(() => IOLog.RequiredRunEventSinkDrained(_logger, name));
            return DrainOutcome.Drained;
        }

        using var deadline = new CancellationTokenSource(binding.Registration.FlushDeadline, _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        Task flush;
        try
        {
            flush = flushable.FlushAsync(linked.Token).AsTask();
        }
        catch (Exception exception)
        {
            flush = Task.FromException(exception);
        }

        try
        {
            // WaitAsync bounds the wait even for a sink that ignores the token; the orphaned flush is observed below.
            await flush.WaitAsync(linked.Token).ConfigureAwait(false);
            SafeLog(() => IOLog.RequiredRunEventSinkDrained(_logger, name));
            return DrainOutcome.Drained;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ObserveLate(flush);
            return DrainOutcome.Drained;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            ObserveLate(flush);
            SafeLog(() => IOLog.RequiredRunEventSinkFlushTimedOut(_logger, name, binding.Registration.FlushDeadline));
            return DrainOutcome.TimedOut;
        }
        catch (Exception exception)
        {
            SafeLog(() => IOLog.RequiredRunEventSinkFlushFaulted(_logger, name, exception.GetType().Name));
            return DrainOutcome.Failed;
        }
    }

    private static void ObserveLate(Task flush) =>
        _ = flush.ContinueWith(
            static task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    private static void SafeLog(Action log)
    {
        try
        {
            log();
        }
        catch (Exception)
        {
            // Logging is observational and never changes a drain outcome.
        }
    }

    private enum DrainOutcome
    {
        Drained,
        TimedOut,
        Failed,
    }
}
