// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Isolates safe diagnostics for one coordinated capture attempt without observing publication or schema content.</summary>
/// <remarks>The scope completes once and disposes in execution-context order. Observer and clock failures never change the coordination result.</remarks>
internal sealed class ToolCatalogCoordinationObservation: IDisposable
{
    private readonly ToolDiscoveryRequest _request;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly AgentKitActivityScope _scope;
    private readonly long? _started;
    private bool _completed;

    /// <summary>Starts correlated diagnostics after the coordinator has validated its request.</summary>
    /// <param name="request">The nonnull coherent run identity evidence.</param>
    /// <param name="timeProvider">The nonnull observation-only clock.</param>
    /// <param name="logger">The nonnull coordinator-category logger.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    internal ToolCatalogCoordinationObservation(ToolDiscoveryRequest request, TimeProvider timeProvider, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _request = request;
        _timeProvider = timeProvider;
        _logger = logger;
        try { _started = timeProvider.GetTimestamp(); }
        catch { /* Missing diagnostic time is not reported as zero. */ }
        _scope = AgentKitActivityScope.Start(AgentKitActivityNames.ToolCatalogCoordinate, ActivityKind.Internal, new ActivityTagsCollection
        {
            { AgentKitTagNames.GenAiOperationName, AgentKitActivityNames.ToolCatalogCoordinate },
            { AgentKitTagNames.TenantId, request.Identity.TenantId.Value },
            { AgentKitTagNames.PrincipalId, request.Identity.PrincipalId.Value },
            { AgentKitTagNames.AgentId, request.AgentId.ToString() },
            { AgentKitTagNames.SessionId, request.SessionId.ToString() },
            { AgentKitTagNames.RunId, request.RunId.ToString() },
        });
        Observe(() => ToolLog.CatalogCoordinationStarted(logger, request.Identity.TenantId, request.Identity.PrincipalId, request.AgentId, request.SessionId, request.RunId));
    }

    /// <summary>Records one truthful terminal result without exposing publication, collision, or schema content.</summary>
    /// <param name="outcome">Exactly captured, merge_rejected, schema_rejected, cancelled, or failed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="outcome"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="outcome"/> is not a defined terminal value.</exception>
    /// <remarks>Repeated completion calls are harmless after input validation; only the first valid terminal result is recorded.</remarks>
    internal void Complete(string outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentException.ThrowIfNotEqual(outcome is "captured" or "merge_rejected" or "schema_rejected" or "cancelled" or "failed", true, nameof(outcome));
        if (_completed) { return; }
        _completed = true;
        var level = outcome switch
        {
            "captured" => LogLevel.Debug,
            "cancelled" => LogLevel.Information,
            "merge_rejected" or "schema_rejected" => LogLevel.Warning,
            _ => LogLevel.Error,
        };
        Observe(() => ToolLog.CatalogCoordinationCompleted(_logger, level, _request.Identity.TenantId, _request.Identity.PrincipalId, _request.AgentId, _request.SessionId, _request.RunId, outcome));
        Observe(() =>
        {
            if (outcome == "captured") { _scope.Activity.SetSuccessful(outcome); }
            else { _scope.Activity.SetFailed(outcome, outcome); }
        });
        var tags = new TagList { { AgentKitTagNames.GenAiOperationName, AgentKitActivityNames.ToolCatalogCoordinate }, { AgentKitTagNames.Outcome, outcome } };
        Observe(() => ToolCatalogCoordinationMetrics.Count.Add(1, tags));
        if (_started is { } started)
        {
            Observe(() =>
            {
                var elapsed = _timeProvider.GetElapsedTime(started);
                if (elapsed >= TimeSpan.Zero) { ToolCatalogCoordinationMetrics.Duration.Record(elapsed.TotalSeconds, tags); }
            });
        }
    }

    /// <summary>Stops the owned activity and restores its captured parent while containing listener failures.</summary>
    /// <remarks>An uncompleted scope records failure instead of leaving an untruthful terminal activity. Repeated disposal is harmless.</remarks>
    public void Dispose()
    {
        if (!_completed) { Complete("failed"); }
        _scope.Dispose();
    }

    private static void Observe(Action observation)
    {
        Debug.Assert(observation is not null, "The coordinator supplies every observation callback.");
        try { observation(); }
        catch { /* Observation cannot change the coordination result. */ }
    }
}
