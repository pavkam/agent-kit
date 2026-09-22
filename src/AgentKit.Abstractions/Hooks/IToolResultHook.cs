// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Runs at <see cref="AgentHookPoints.ToolResult"/>, after one tool call reaches a terminal result and before history projection.</summary>
/// <remarks>
/// Implementations may set <see cref="ToolResultHookEventArgs.ContentReplacement"/> with bounded normalized content.
/// They must not change terminal status, security evidence, or side-effect certainty on the authoritative result.
/// </remarks>
public interface IToolResultHook
{
    /// <summary>Observes or adjusts bounded terminal tool-result content.</summary>
    /// <param name="eventArgs">The terminal result and permitted writable replacement slot.</param>
    /// <param name="invocation">The hook invocation context for this registration.</param>
    /// <param name="cancellationToken">Signals cancellation; cancellation propagates to the tool batch.</param>
    /// <returns>A task that completes when the hook finishes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="eventArgs"/> or <paramref name="invocation"/> is null.</exception>
    public ValueTask InvokeAsync(
        ToolResultHookEventArgs eventArgs,
        HookInvocationContext invocation,
        CancellationToken cancellationToken = default);
}
