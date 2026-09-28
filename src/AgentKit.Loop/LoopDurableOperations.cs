// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

/// <summary>Names and versions the recoverable operations this loop can journal.</summary>
/// <remarks>
/// A durability profile enables operations by name, so these values are part of the loop's configuration surface: a
/// profile that does not list a name leaves that boundary undurable rather than silently journaling it. The versions
/// are bumped whenever the encoded state shape changes, because recovery refuses a payload version it cannot read
/// rather than reinterpreting old bytes under new rules.
/// </remarks>
public static class LoopDurableOperations
{
    /// <summary>Gets the operation name for one turn's complete model attempt.</summary>
    /// <value>The name a durability profile must enable before model attempts are journaled.</value>
    public static DurableOperationName ModelRequest { get; } = new("agentkit.loop.model_request");

    /// <summary>Gets the operation name for one requested tool call's invocation.</summary>
    /// <value>The name a durability profile must enable before tool calls are journaled.</value>
    public static DurableOperationName ToolCall { get; } = new("agentkit.loop.tool_call");

    /// <summary>Gets the published version of the model-request state shape.</summary>
    /// <value>The version recorded on every model-request declaration.</value>
    public static DurableOperationVersion ModelRequestVersion { get; } = new("v1");

    /// <summary>Gets the published version of the tool-call state shape.</summary>
    /// <value>The version recorded on every tool-call declaration.</value>
    public static DurableOperationVersion ToolCallVersion { get; } = new("v1");

    /// <summary>Gets every operation name this loop can journal.</summary>
    /// <value>The complete additive set a profile may enable; the loop journals no other name.</value>
    public static ImmutableArray<DurableOperationName> All { get; } = [ModelRequest, ToolCall];
}
