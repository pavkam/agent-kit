// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Event arguments for <see cref="AgentHookPoints.ToolResult"/>: one authoritative terminal tool result before
/// history projection. Hooks may replace bounded normalized content only.
/// </summary>
/// <remarks>
/// This is a mutating point with fail-operation failure policy. Writable state is limited to
/// <see cref="ContentReplacement"/>; identity, authorization, terminal status, and side-effect certainty remain
/// read-only on <see cref="OriginalResult"/>.
/// </remarks>
public sealed class ToolResultHookEventArgs: AgentHookEventArgs, IAgentScopedHookStage
{
    /// <summary>Initializes tool-result hook arguments.</summary>
    /// <param name="dispatch">The point identity, dispatch identity, causality, and timing facts for this dispatch.</param>
    /// <param name="agentId">The owning agent.</param>
    /// <param name="sessionId">The owning session.</param>
    /// <param name="runId">The active run.</param>
    /// <param name="result">The authoritative terminal result produced by the tool runtime.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dispatch"/> or <paramref name="result"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default.</exception>
    public ToolResultHookEventArgs(
        HookDispatchMetadata dispatch,
        AgentId agentId,
        SessionId sessionId,
        RunId runId,
        ToolCallResult result)
        : base(dispatch)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        ArgumentNullException.ThrowIfNull(result);
        AgentId = agentId;
        SessionId = sessionId;
        RunId = runId;
        OriginalResult = result;
    }

    /// <inheritdoc/>
    public AgentId AgentId { get; }

    /// <inheritdoc/>
    public SessionId? SessionId { get; }

    /// <summary>Gets the active run identity.</summary>
    public RunId RunId { get; }

    /// <summary>Gets the authoritative terminal result under observation.</summary>
    /// <value>Read-only terminal evidence; hooks must not mutate this instance.</value>
    public ToolCallResult OriginalResult { get; }

    /// <summary>Gets or sets optional bounded replacement normalized content.</summary>
    /// <value>Null when the hook makes no content change.</value>
    public ToolResultContentReplacement? ContentReplacement { get; set; }
}
