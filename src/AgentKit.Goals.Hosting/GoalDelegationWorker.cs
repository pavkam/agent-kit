// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Hosting;

/// <summary>Drains committed delegation intents for the host: signalled ones immediately and durable ones on startup and on a schedule.</summary>
/// <remarks>
/// <para>
/// A child never runs inside the delegating call. The local dispatcher commits a durable intent and this worker, activated
/// by the host after the engine is composed, claims and runs it through the public engine surface. The signal is an
/// optimization: the periodic scan of the configured profiles' durable intents is what recovers work after process loss, a
/// full wake-up queue, or a store that only this host can reach.
/// </para>
/// <para>
/// Every intent is drained independently; a fault in one is logged and never stops the loop. Concurrency is bounded by the
/// configured slots, and a run that waits on its own child releases its slot while it waits.
/// </para>
/// </remarks>
public sealed class GoalDelegationWorker: BackgroundService, IAsyncDisposable
{
    private readonly DelegationIntentQueue _queue;
    private readonly DelegationIntentProcessor _processor;
    private readonly IGoalStoreSelector _stores;
    private readonly DelegationWorkerSlots _slots;
    private readonly TimeProvider _time;
    private readonly GoalWorkerOptions _options;
    private readonly ILogger<GoalDelegationWorker> _logger;
    private readonly ConcurrentDictionary<Task, byte> _running = new();
    private int _started;

    internal GoalDelegationWorker(
        DelegationIntentQueue queue,
        DelegationIntentProcessor processor,
        IGoalStoreSelector stores,
        DelegationWorkerSlots slots,
        TimeProvider time,
        IOptions<GoalWorkerOptions> options,
        ILogger<GoalDelegationWorker>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(processor);
        ArgumentNullException.ThrowIfNull(stores);
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(options);
        _queue = queue;
        _processor = processor;
        _stores = stores;
        _slots = slots;
        _time = time;
        _options = options.Value;
        _logger = logger ?? NullLogger<GoalDelegationWorker>.Instance;
    }

    /// <summary>Starts the worker once; later calls are ignored, so a host that starts it and a signal that activates it cannot start it twice.</summary>
    /// <param name="cancellationToken">Cancels startup.</param>
    /// <returns>A task that completes when the worker has started.</returns>
    public override Task StartAsync(CancellationToken cancellationToken) =>
        Interlocked.Exchange(ref _started, 1) == 0 ? base.StartAsync(cancellationToken) : Task.CompletedTask;

    /// <summary>Stops the worker and waits for every claimed attempt to settle.</summary>
    /// <returns>A task that completes when the worker has stopped.</returns>
    /// <remarks>A standalone engine has no host to stop the worker, so disposing the owning provider calls this.</remarks>
    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Gets the number of intents currently being drained, for diagnostics and tests.</summary>
    internal int InFlight => _running.Count;

    /// <summary>Scans the configured profiles' durable intents once and drains each.</summary>
    /// <param name="cancellationToken">Stops the scan.</param>
    /// <returns>A task that completes when every discovered intent was handed to the drain.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    internal async Task ScanOnceAsync(CancellationToken cancellationToken)
    {
        foreach (var profile in _options.Profiles)
        {
            await ScanProfileAsync(profile, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        WorkerObservation.Safe(() => GoalWorkerLog.WorkerStarted(_logger, _slots.Capacity, _options.Profiles.Count));
        try
        {
            await Task.WhenAll(ConsumeAsync(stoppingToken), ScanLoopAsync(stoppingToken)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Stopping is the normal end of the loop; claimed attempts were settled by their own drains.
        }
        finally
        {
            await Task.WhenAll(_running.Keys).ConfigureAwait(false);
            WorkerObservation.Safe(() => GoalWorkerLog.WorkerStopped(_logger));
        }
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        await foreach (var intent in _queue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            Start(intent.Request, intent.Child.Goal.Id, cancellationToken);
        }
    }

    private async Task ScanLoopAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            await ScanOnceAsync(cancellationToken).ConfigureAwait(false);
            await Task.Delay(_options.ScanInterval, _time, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ScanProfileAsync(GoalProfileReference profile, CancellationToken cancellationToken)
    {
        try
        {
            var selection = await _stores.SelectAsync(new GoalStoreSelectionRequest(profile), cancellationToken).ConfigureAwait(false);
            if (selection is not GoalStoreSelected { Store: var store })
            {
                WorkerObservation.Safe(() => GoalWorkerLog.ScanFailed(_logger, profile.Key, "no_store"));
                return;
            }

            if (!store.Descriptor.SupportsIntentDiscovery)
            {
                return;
            }

            long after = 0;
            while (true)
            {
                var page = await store.ReadIntentsAsync(new GoalIntentScanRequest(_options.ScannerId, after, _options.ScanPageSize), cancellationToken).ConfigureAwait(false);
                if (page is not GoalPage { Items: var items, Next: var next })
                {
                    WorkerObservation.Safe(() => GoalWorkerLog.ScanFailed(_logger, profile.Key, "rejected"));
                    return;
                }

                foreach (var record in items)
                {
                    if (record.Delegation is { } delegation)
                    {
                        Start(delegation, record.Goal.Id, cancellationToken);
                    }
                }

                if (next is not { } cursor)
                {
                    return;
                }

                after = cursor;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            WorkerObservation.Safe(() => GoalWorkerLog.ScanFailed(_logger, profile.Key, exception.GetType().Name));
        }
    }

    private void Start(DelegationRequest request, GoalId childGoalId, CancellationToken cancellationToken)
    {
        var task = Task.Run(async () =>
        {
            try
            {
                _ = await _processor.ProcessAsync(request, childGoalId, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The worker is stopping; the processor already settled any claimed attempt.
            }
            catch (Exception)
            {
                // The processor logged the fault; one failing intent never stops the worker.
            }
        }, CancellationToken.None);
        _ = _running.TryAdd(task, 0);
        _ = task.ContinueWith(completed => _running.TryRemove(completed, out _), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
