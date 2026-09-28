// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Diagnostics;

/// <summary>Owns one built-in tool leaf invocation activity and counter without logging protected arguments.</summary>
public sealed class ToolLeafObservation: IDisposable
{
    private static readonly Counter<long> _operationCounter =
        AgentKitDiagnostics.Metrics.CreateCounter<long>(AgentKitMetricNames.ToolLeafOperationCount);

    private readonly AgentKitActivityScope _scope;
    private bool _completed;

    private ToolLeafObservation(AgentKitActivityScope scope) => _scope = scope;

    /// <summary>Starts one GenAI-compatible tool execution observation.</summary>
    /// <param name="toolId">The stable tool identity.</param>
    /// <returns>A scope that must be completed exactly once.</returns>
    /// <exception cref="ArgumentException"><paramref name="toolId"/> is default.</exception>
    public static ToolLeafObservation Start(ToolId toolId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(toolId, default, nameof(toolId));
        var scope = AgentKitActivityScope.Start(
            AgentKitActivityNames.ExecuteTool,
            ActivityKind.Internal,
            new ActivityTagsCollection
            {
                { AgentKitTagNames.GenAiOperationName, AgentKitActivityNames.ExecuteTool },
                { AgentKitTagNames.ToolId, toolId.ToString() },
            });
        return new ToolLeafObservation(scope);
    }

    /// <summary>Marks the invocation terminal and records a bounded outcome counter.</summary>
    /// <param name="outcome">The bounded semantic outcome label.</param>
    /// <param name="status">The terminal activity status.</param>
    /// <exception cref="ArgumentException"><paramref name="outcome"/> is blank.</exception>
    /// <exception cref="InvalidOperationException">The scope was already completed.</exception>
    public void Complete(string outcome, ActivityStatusCode status = ActivityStatusCode.Ok)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        ObjectDisposedException.ThrowIf(_completed, this);
        _completed = true;
        _ = (_scope.Activity?.SetStatus(status));
        _ = (_scope.Activity?.SetTag(AgentKitTagNames.Outcome, outcome));
        _operationCounter.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_completed)
        {
            Complete("faulted", ActivityStatusCode.Error);
        }

        _scope.Dispose();
    }
}
