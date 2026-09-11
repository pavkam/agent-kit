// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Isolates content-free observation of canonical schema compilation and validation.</summary>
/// <remarks>Scopes own no schema or run evidence. Parent activity context carries causality without inventing semantic identities.</remarks>
internal sealed class ToolSchemaObservation: IDisposable
{
    private readonly string _operation;
    private readonly TimeProvider _clock;
    private readonly ILogger _logger;
    private readonly AgentKitActivityScope _scope;
    private readonly long? _started;
    private bool _completed;

    /// <summary>Starts a bounded schema operation after the caller has validated all inputs.</summary>
    /// <param name="operation">Exactly the shared compile or validate activity name.</param>
    /// <param name="clock">The nonnull observation-only clock.</param>
    /// <param name="logger">The nonnull emitting-type logger.</param>
    /// <exception cref="ArgumentNullException">A collaborator or operation is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="operation"/> is not a defined schema stage.</exception>
    internal ToolSchemaObservation(string operation, TimeProvider clock, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(operation); ArgumentNullException.ThrowIfNull(clock); ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNotEqual(operation is AgentKitActivityNames.ToolSchemaCompile or AgentKitActivityNames.ToolSchemaValidate, true, nameof(operation));
        _operation = operation; _clock = clock; _logger = logger;
        try { _started = clock.GetTimestamp(); } catch { /* Missing observation time remains unknown. */ }
        _scope = AgentKitActivityScope.Start(operation, ActivityKind.Internal, new ActivityTagsCollection { { AgentKitTagNames.GenAiOperationName, operation } });
        Observe(() => ToolLog.SchemaStarted(logger, operation));
    }

    /// <summary>Records the first valid terminal outcome without schema, instance, profile, or exception content.</summary>
    /// <param name="outcome">Exactly accepted, configuration_rejected, invalid, resource_limited, cancelled, or failed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="outcome"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="outcome"/> is undefined.</exception>
    internal void Complete(string outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentException.ThrowIfNotEqual(outcome is "accepted" or "configuration_rejected" or "invalid" or "resource_limited" or "cancelled" or "failed", true, nameof(outcome));
        if (_completed) { return; }
        _completed = true;
        Observe(() => ToolLog.SchemaCompleted(_logger, outcome is "failed" ? LogLevel.Error : outcome is "accepted" ? LogLevel.Debug : LogLevel.Information, _operation, outcome));
        Observe(() => { if (outcome == "accepted") { _scope.Activity.SetSuccessful(outcome); } else { _scope.Activity.SetFailed(outcome, outcome); } });
        var tags = new TagList { { AgentKitTagNames.GenAiOperationName, _operation }, { AgentKitTagNames.Outcome, outcome } };
        Observe(() => ToolSchemaMetrics.Count.Add(1, tags));
        if (_started is { } started)
        {
            Observe(() => { var elapsed = _clock.GetElapsedTime(started); if (elapsed >= TimeSpan.Zero) { ToolSchemaMetrics.Duration.Record(elapsed.TotalSeconds, tags); } });
        }
    }

    /// <summary>Records uncompleted work as failure and restores the captured activity parent.</summary>
    /// <remarks>Repeated disposal is harmless; listener failure never changes validation.</remarks>
    public void Dispose()
    {
        if (!_completed) { Complete("failed"); }
        _scope.Dispose();
    }

    private static void Observe(Action action)
    {
        Debug.Assert(action is not null, "The schema operation supplies its diagnostic callback.");
        try { action(); } catch { /* Instrumentation is observational only. */ }
    }
}
