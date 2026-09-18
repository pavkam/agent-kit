// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The stable identities of the first-party hook points the agent loop dispatches.</summary>
/// <remarks>
/// Each point has one narrow hook interface and one event-argument type. Third-party features define their own
/// points in their owning assemblies; this catalog names only the boundaries <c>AgentKit.Loop</c> owns.
/// </remarks>
public static class AgentHookPoints
{
    /// <summary>
    /// Dispatched once per run after the model is resolved and before the first turn. Read-only; failures may be
    /// isolated.
    /// </summary>
    public static HookPointId RunStarted { get; } = new("agentkit.run.started");

    /// <summary>
    /// Dispatched before each model request with the assembled context. Hooks may narrow the request settings;
    /// a failure fails the turn.
    /// </summary>
    public static HookPointId BeforeModelRequest { get; } = new("agentkit.model.request.before");

    /// <summary>
    /// Dispatched before each tool invocation with the validated call. Hooks may rewrite the arguments or veto the
    /// call with a typed reason; a failure fails the turn.
    /// </summary>
    public static HookPointId BeforeToolInvocation { get; } = new("agentkit.tool.invocation.before");
}
