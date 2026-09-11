// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Owns one best-effort hub activity and its terminal log and aggregate measurements.</summary>
/// <remarks>Clock and observer failures are isolated. No content, sequence, identity, or exception text enters metric dimensions.</remarks>
internal sealed class RunEventHubObservation: IDisposable
{
    private readonly RunEventHubOperation _operation;
    private readonly AgentId _agentId;
    private readonly SessionId _sessionId;
    private readonly RunId _runId;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly AgentKitActivityScope _scope;
    private readonly long? _started;
    private RunEventHubOutcome _outcome = RunEventHubOutcome.Faulted;
    private bool _disposed;

    /// <summary>Starts one observation only after caller arguments and immutable correlation have been validated.</summary>
    /// <param name="operation">The defined bounded operation.</param>
    /// <param name="agentId">The nondefault accepted agent.</param>
    /// <param name="sessionId">The nondefault session.</param>
    /// <param name="runId">The nondefault accepted run.</param>
    /// <param name="timeProvider">The nonnull observational clock.</param>
    /// <param name="logger">The nonnull structural logger.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default or <paramref name="operation"/> is undefined.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> or <paramref name="logger"/> is null.</exception>
    internal RunEventHubObservation(RunEventHubOperation operation, AgentId agentId, SessionId sessionId, RunId runId, TimeProvider timeProvider, ILogger logger)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(operation);
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _operation = operation; _agentId = agentId; _sessionId = sessionId; _runId = runId;
        _timeProvider = timeProvider; _logger = logger;
        _scope = AgentKitActivityScope.Start(AgentKitActivityNames.RunEventHub, ActivityKind.Internal,
            new ActivityTagsCollection
            {
                { AgentKitTagNames.RunEventHubOperation, operation.ToString() },
                { AgentKitTagNames.AgentId, agentId.ToString() },
                { AgentKitTagNames.SessionId, sessionId.ToString() },
                { AgentKitTagNames.RunId, runId.ToString() },
            });
        try { _started = timeProvider.GetTimestamp(); }
        catch { /* Observation cannot change delivery. */ }
    }

    /// <summary>Captures the exact local outcome before disposal publishes observation.</summary>
    /// <param name="outcome">The defined terminal observation outcome.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined.</exception>
    internal void Finish(RunEventHubOutcome outcome)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(outcome);
        _outcome = outcome;
    }

    /// <summary>Emits independent safe signals and restores activity parentage exactly once.</summary>
    /// <remarks>Every observer and clock failure is contained; disposal never changes queue state or caller outcomes.</remarks>
    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;
        var operation = _operation.ToString();
        var outcome = _outcome.ToString();
        var error = _outcome is not (RunEventHubOutcome.Succeeded or RunEventHubOutcome.Abandoned);
        try
        {
            _ = _scope.Activity?.SetTag(AgentKitTagNames.Outcome, outcome);
            _ = _scope.Activity?.SetStatus(error ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
        }
        catch { /* Activity enrichment is observational. */ }
        try
        {
            RunEventHubLog.Completed(_logger,
                _outcome == RunEventHubOutcome.Faulted ? LogLevel.Error : error ? LogLevel.Warning : LogLevel.Debug,
                operation, outcome, _agentId, _sessionId, _runId);
        }
        catch { /* Logging is observational. */ }
        TimeSpan? elapsed = null;
        try
        {
            if (_started is { } started && _timeProvider.GetElapsedTime(started) is { } duration && duration >= TimeSpan.Zero) { elapsed = duration; }
        }
        catch { /* A failed clock omits duration, not the count. */ }
        try { RunEventHubMetrics.Record(_operation, _outcome, elapsed); }
        catch { /* Metrics are observational. */ }
        _scope.Dispose();
    }
}
