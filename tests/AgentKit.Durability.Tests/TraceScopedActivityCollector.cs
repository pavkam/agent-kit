// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Tests;

using System.Collections.Concurrent;

/// <summary>
/// Collects terminal statuses of one named AgentKit activity that share the trace of a test-owned root activity, so concurrent
/// tests that emit the same operation name never contaminate the observation.
/// </summary>
/// <remarks>
/// Construction starts a root activity on the calling async flow; every activity started by the code under test in that flow
/// inherits its trace. Dispose stops the root and unregisters the process-wide listener.
/// </remarks>
internal sealed class TraceScopedActivityCollector: IDisposable
{
    private readonly ActivitySource _source = new($"AgentKit.Durability.Tests.{Guid.NewGuid():N}");
    private readonly ActivityListener _listener;
    private readonly ConcurrentQueue<ActivityStatusCode> _statuses = new();
    private readonly Activity _root;

    /// <summary>Starts the listener and the trace root for one operation name.</summary>
    /// <param name="operationName">The exact activity name whose terminal statuses are retained.</param>
    /// <exception cref="ArgumentException"><paramref name="operationName"/> is null, empty, or whitespace.</exception>
    public TraceScopedActivityCollector(string operationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        var scopeName = _source.Name;
        ActivityTraceId traceId = default;
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentKitDiagnostics.ActivitySourceName || source.Name == scopeName,
            Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == traceId && activity.OperationName == operationName)
                {
                    _statuses.Enqueue(activity.Status);
                }
            },
        };
        ActivitySource.AddActivityListener(_listener);
        _root = _source.StartActivity("scope") ?? throw new InvalidOperationException("The scope activity was not sampled.");
        traceId = _root.TraceId;
    }

    /// <summary>Returns the terminal statuses recorded so far in arrival order.</summary>
    /// <returns>A point-in-time copy.</returns>
    public IReadOnlyList<ActivityStatusCode> Snapshot() => [.. _statuses];

    /// <summary>Stops the root activity and unregisters the listener.</summary>
    public void Dispose()
    {
        _root.Dispose();
        _listener.Dispose();
        _source.Dispose();
    }
}
