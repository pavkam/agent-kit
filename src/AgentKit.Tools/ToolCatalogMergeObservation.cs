// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Isolates safe diagnostics for the two catalog merge boundaries without observing descriptor content.</summary>
/// <remarks>Single-owner scopes complete once and dispose in execution-context order. Observer and clock failures never change selection or rejection.</remarks>
internal sealed class ToolCatalogMergeObservation: IDisposable
{
    private readonly string _operation;
    private readonly ToolDiscoveryRequest _request;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly AgentKitActivityScope _scope;
    private readonly long? _started;
    private bool _completed;

    /// <summary>Starts correlated diagnostics after the owning operation has validated its inputs.</summary>
    /// <param name="policy">True for policy resolution; false for catalog merge coordination.</param>
    /// <param name="request">The nonnull coherent run identity evidence.</param>
    /// <param name="timeProvider">The nonnull observation-only clock.</param>
    /// <param name="logger">The nonnull logger categorized by the emitting runtime type.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    internal ToolCatalogMergeObservation(bool policy, ToolDiscoveryRequest request, TimeProvider timeProvider, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _request = request;
        _timeProvider = timeProvider;
        _logger = logger;
        _operation = policy ? AgentKitActivityNames.ToolCatalogMergePolicy : AgentKitActivityNames.ToolCatalogMerge;
        try { _started = timeProvider.GetTimestamp(); }
        catch { /* Missing diagnostic time is not reported as zero. */ }
        _scope = AgentKitActivityScope.Start(_operation, ActivityKind.Internal, new ActivityTagsCollection
        {
            { AgentKitTagNames.GenAiOperationName, _operation },
            { AgentKitTagNames.TenantId, request.Identity.TenantId.Value },
            { AgentKitTagNames.PrincipalId, request.Identity.PrincipalId.Value },
            { AgentKitTagNames.AgentId, request.AgentId.ToString() },
            { AgentKitTagNames.SessionId, request.SessionId.ToString() },
            { AgentKitTagNames.RunId, request.RunId.ToString() },
        });
        Observe(() => ToolLog.CatalogMergeStarted(logger, _operation, request.Identity.TenantId, request.Identity.PrincipalId, request.AgentId, request.SessionId, request.RunId));
    }

    /// <summary>Records one truthful terminal result without exposing collision or descriptor content.</summary>
    /// <param name="outcome">Exactly selected, rejected, cancelled, or failed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="outcome"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="outcome"/> is not a defined terminal value.</exception>
    /// <remarks>Repeated completion calls are harmless after input validation; only the first valid terminal result is recorded.</remarks>
    internal void Complete(string outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentException.ThrowIfNotEqual(outcome is "selected" or "rejected" or "cancelled" or "failed", true, nameof(outcome));
        if (_completed) { return; }
        _completed = true;
        var level = outcome switch { "selected" => LogLevel.Debug, "cancelled" => LogLevel.Information, "rejected" => LogLevel.Warning, _ => LogLevel.Error };
        Observe(() => ToolLog.CatalogMergeCompleted(_logger, level, _operation, _request.Identity.TenantId, _request.Identity.PrincipalId, _request.AgentId, _request.SessionId, _request.RunId, outcome));
        Observe(() =>
        {
            if (outcome == "selected") { _scope.Activity.SetSuccessful(outcome); }
            else { _scope.Activity.SetFailed(outcome, outcome); }
        });
        var tags = new TagList { { AgentKitTagNames.GenAiOperationName, _operation }, { AgentKitTagNames.Outcome, outcome } };
        Observe(() => ToolCatalogMergeMetrics.Count.Add(1, tags));
        if (_started is { } started)
        {
            Observe(() =>
            {
                var elapsed = _timeProvider.GetElapsedTime(started);
                if (elapsed >= TimeSpan.Zero) { ToolCatalogMergeMetrics.Duration.Record(elapsed.TotalSeconds, tags); }
            });
        }
    }

    /// <summary>Stops the owned activity and restores its captured parent while containing listener failures.</summary>
    /// <remarks>Uncompleted scopes record failure instead of leaving an untruthful terminal activity. Repeated disposal is harmless.</remarks>
    public void Dispose()
    {
        if (!_completed) { Complete("failed"); }
        _scope.Dispose();
    }

    private static void Observe(Action observation)
    {
        Debug.Assert(observation is not null, "The merge boundary supplies every observation callback.");
        try { observation(); }
        catch { /* Observation cannot change the captured graph or decision. */ }
    }
}
