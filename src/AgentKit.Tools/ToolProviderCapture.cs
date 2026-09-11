// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Collections.Frozen;

/// <summary>Pins a source publication to exact borrowed invoker instances and drains leases before owned cleanup.</summary>
/// <remarks>
/// This run-owned capture performs no provider lookup or invocation. It normalizes dictionary
/// comparers at construction and never rereads live descriptor metadata. Each successful acquisition
/// has an independent lease. Closure rejects new acquisitions, retains outstanding bindings, and
/// releases an optional source lifetime once the last lease closes. All state transitions are
/// synchronized; callbacks, cleanup, and diagnostics run outside the state gate.
/// </remarks>
public sealed class ToolProviderCapture: IToolProviderCapture
{
    private readonly FrozenDictionary<ToolIdentity, (ToolDescriptor Tool, IToolInvoker Invoker)> _bindings;
    private readonly IAsyncDisposable? _lifetime;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ToolProviderCapture> _logger;
    private readonly Lock _gate = new();
    private readonly TaskCompletionSource _disposal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _closed;
    private bool _cleanupStarted;
    private long _leases;

    /// <summary>Captures a complete binding graph and takes ownership of its optional source lifetime after validation.</summary>
    /// <param name="snapshot">The nonnull immutable source publication.</param>
    /// <param name="invokers">The exact nonnull binding for every descriptor identity, with no missing, extra, default, or duplicate normalized keys.</param>
    /// <param name="lifetime">Optional owned source resources released after closure and the final lease; null means all invokers remain externally owned. The lifetime must not own or await this capture or its leases.</param>
    /// <param name="timeProvider">The nonnull replaceable clock used only for diagnostic duration.</param>
    /// <param name="logger">The nonnull capture-specific logger receiving safe lifecycle metadata.</param>
    /// <exception cref="ArgumentNullException">A required reference or a binding in <paramref name="invokers"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A key in <paramref name="invokers"/> is default.</exception>
    /// <exception cref="ArgumentException">The normalized binding keyset differs from the publication or normalization reveals duplicate identities.</exception>
    /// <remarks>
    /// Rejected construction leaves lifetime ownership with the caller. This class never disposes
    /// individual invokers; the supplied lifetime or their external host owns them. Publishers must
    /// bind instances from the same exact source version before constructing this capture.
    /// </remarks>
    public ToolProviderCapture(
        ToolProviderSnapshot snapshot,
        ImmutableDictionary<ToolIdentity, IToolInvoker> invokers,
        IAsyncDisposable? lifetime,
        TimeProvider timeProvider,
        ILogger<ToolProviderCapture> logger)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(invokers);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        var captured = new Dictionary<ToolIdentity, IToolInvoker>();
        foreach (var pair in invokers)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(pair.Key, default, nameof(invokers));
            ArgumentNullException.ThrowIfNull(pair.Value, nameof(invokers));
            ArgumentException.ThrowIfNotEqual(captured.TryAdd(pair.Key, pair.Value), true, nameof(invokers));
        }
        ArgumentException.ThrowIfNotEqual(captured.Count, snapshot.Tools.Length, nameof(invokers));
        var bindings = new Dictionary<ToolIdentity, (ToolDescriptor Tool, IToolInvoker Invoker)>();
        foreach (var tool in snapshot.Tools)
        {
            var identity = new ToolIdentity(tool.Id, tool.Version);
            ArgumentException.ThrowIfNotEqual(captured.ContainsKey(identity), true, nameof(invokers));
            bindings.Add(identity, (tool, captured[identity]));
        }

        _bindings = bindings.ToFrozenDictionary();
        Snapshot = snapshot;
        _lifetime = lifetime;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public ToolProviderSnapshot Snapshot { get; }

    /// <inheritdoc/>
    public ValueTask<ToolInvokerLeaseResult> AcquireInvokerAsync(ToolIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(identity, default);
        const string operation = AgentKitActivityNames.ToolInvokerAcquire;
        var started = TryGetTimestamp();
        using var scope = Start(operation, identity);
        try
        {
            ToolInvokerLeaseResult result;
            lock (_gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_closed)
                {
                    result = new ToolInvokerUnavailable(identity, "The source capture is closed.");
                }
                else if (_bindings.TryGetValue(identity, out var binding))
                {
                    var lease = new ToolInvokerLease(binding.Tool, Snapshot.SourceVersion, binding.Invoker, ReleaseAsync);
                    result = new ToolInvokerAcquired(lease);
                    _leases = checked(_leases + 1);
                }
                else
                {
                    result = new ToolInvokerUnavailable(identity, "The exact tool identity is absent from the captured source publication.");
                }
            }

            if (result is ToolInvokerUnavailable)
            {
                Observe(() => ToolLog.CaptureInvokerUnavailable(_logger, Snapshot.SourceId, Snapshot.SourceVersion, identity.Id, identity.Version));
                Complete(scope.Activity, operation, "unavailable", started);
            }
            else
            {
                Succeeded(scope.Activity, operation, "acquired", started);
            }
            return ValueTask.FromResult(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Observe(() => ToolLog.CaptureAcquisitionCancelled(_logger, Snapshot.SourceId, Snapshot.SourceVersion));
            Complete(scope.Activity, operation, "cancelled", started);
            throw;
        }
        catch (Exception error)
        {
            Failed(scope.Activity, operation, started, error);
            throw;
        }
    }

    /// <summary>Closes acquisition and drains every outstanding lease before completing owned source cleanup.</summary>
    /// <returns>The shared cleanup completion; an owned lifetime failure propagates unchanged to every waiter.</returns>
    /// <remarks>
    /// Concurrent and repeated calls do not retry cleanup. Callers release held leases before awaiting
    /// closure on the same control path. This operation never disposes borrowed invokers directly.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        const string operation = AgentKitActivityNames.ToolProviderCaptureClose;
        var started = TryGetTimestamp();
        using var scope = Start(operation);
        try
        {
            bool cleanup;
            lock (_gate)
            {
                _closed = true;
                cleanup = TryStartCleanup();
            }
            if (cleanup)
            {
                _ = DisposeResourcesAsync();
            }
            await _disposal.Task.ConfigureAwait(false);
            Succeeded(scope.Activity, operation, "closed", started);
        }
        catch (Exception error)
        {
            Failed(scope.Activity, operation, started, error);
            throw;
        }
    }

    private async ValueTask ReleaseAsync()
    {
        const string operation = AgentKitActivityNames.ToolInvokerRelease;
        var started = TryGetTimestamp();
        using var scope = Start(operation);
        try
        {
            bool cleanup;
            lock (_gate)
            {
                Debug.Assert(_leases > 0, "Each acquired lease releases its registered lifetime exactly once.");
                _leases--;
                cleanup = TryStartCleanup();
            }
            if (cleanup)
            {
                _ = DisposeResourcesAsync();
                await _disposal.Task.ConfigureAwait(false);
            }
            Succeeded(scope.Activity, operation, "released", started);
        }
        catch (Exception error)
        {
            Failed(scope.Activity, operation, started, error);
            throw;
        }
    }

    private bool TryStartCleanup()
    {
        Debug.Assert(_gate.IsHeldByCurrentThread, "Cleanup ownership changes only while holding the capture state gate.");
        if (!_closed || _leases != 0 || _cleanupStarted)
        {
            return false;
        }
        _cleanupStarted = true;
        return true;
    }

    private async Task DisposeResourcesAsync()
    {
        const string operation = AgentKitActivityNames.ToolProviderCaptureDisposeResources;
        var started = TryGetTimestamp();
        Exception? failure = null;
        using (var scope = Start(operation))
        {
            try
            {
                if (_lifetime is not null)
                {
                    await _lifetime.DisposeAsync().ConfigureAwait(false);
                }
                Succeeded(scope.Activity, operation, "disposed", started);
            }
            catch (Exception error)
            {
                Failed(scope.Activity, operation, started, error);
                failure = error;
            }
        }
        _ = failure is null ? _disposal.TrySetResult() : _disposal.TrySetException(failure);
    }

    private AgentKitActivityScope Start(string operation, ToolIdentity? identity = null)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "Every capture stage has a stable shared operation name.");
        var tags = new ActivityTagsCollection
        {
            { AgentKitTagNames.GenAiOperationName, operation },
            { AgentKitTagNames.ToolSourceId, Snapshot.SourceId.Value },
            { AgentKitTagNames.ToolSourceVersion, Snapshot.SourceVersion.Value },
        };
        if (identity is { } tool)
        {
            tags.Add(AgentKitTagNames.ToolId, tool.Id.Value);
            tags.Add(AgentKitTagNames.ToolVersion, tool.Version.Value);
        }
        var scope = AgentKitActivityScope.Start(operation, ActivityKind.Internal, tags);
        Observe(() => ToolLog.CaptureOperationStarted(_logger, operation, Snapshot.SourceId, Snapshot.SourceVersion));
        return scope;
    }

    private void Succeeded(Activity? activity, string operation, string outcome, long? started)
    {
        Debug.Assert(outcome is "acquired" or "closed" or "released" or "disposed", "Successful capture outcomes have a closed vocabulary.");
        Observe(() => ToolLog.CaptureOperationCompleted(_logger, operation, outcome, Snapshot.SourceId, Snapshot.SourceVersion));
        Complete(activity, operation, outcome, started);
    }

    private void Failed(Activity? activity, string operation, long? started, Exception error)
    {
        Debug.Assert(error is not null, "Failure reporting receives the caught exception without copying its content.");
        var errorType = error.GetType().FullName ?? error.GetType().Name;
        Observe(() => ToolLog.CaptureOperationFailed(_logger, operation, Snapshot.SourceId, Snapshot.SourceVersion, errorType));
        Complete(activity, operation, "failed", started, errorType);
    }

    private void Complete(Activity? activity, string operation, string outcome, long? started, string? errorType = null)
    {
        Debug.Assert(operation is AgentKitActivityNames.ToolInvokerAcquire or AgentKitActivityNames.ToolInvokerRelease
            or AgentKitActivityNames.ToolProviderCaptureClose or AgentKitActivityNames.ToolProviderCaptureDisposeResources,
            "Capture metrics use only the shared bounded operation names.");
        Debug.Assert(outcome is "acquired" or "unavailable" or "cancelled" or "closed" or "released" or "disposed" or "failed", "Capture metrics use a bounded outcome vocabulary.");
        Observe(() =>
        {
            if (outcome is "unavailable" or "cancelled" or "failed")
            {
                activity.SetFailed(outcome, errorType ?? outcome);
            }
            else
            {
                activity.SetSuccessful(outcome);
            }
        });
        var tags = new TagList { { AgentKitTagNames.GenAiOperationName, operation }, { AgentKitTagNames.Outcome, outcome } };
        Observe(() => ToolProviderCaptureMetrics.Count.Add(1, tags));
        if (started is { } timestamp)
        {
            Observe(() =>
            {
                var elapsed = _timeProvider.GetElapsedTime(timestamp);
                if (elapsed >= TimeSpan.Zero)
                {
                    ToolProviderCaptureMetrics.Duration.Record(elapsed.TotalSeconds, tags);
                }
            });
        }
    }

    private long? TryGetTimestamp()
    {
        try
        {
            return _timeProvider.GetTimestamp();
        }
        catch
        {
            return null;
        }
    }

    private static void Observe(Action observation)
    {
        Debug.Assert(observation is not null, "The capture supplies every diagnostic callback.");
        try
        {
            observation();
        }
        catch
        {
            // Observers and clocks never acquire, release, or change ownership.
        }
    }
}
