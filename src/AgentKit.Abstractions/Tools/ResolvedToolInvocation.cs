// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The result of one <see cref="IToolInvoker"/> attempt: the exact tool
/// reference the invoker resolved (or left unresolved) the requested call
/// against, the captured projection-policy reference in effect for it, and
/// the terminal invocation outcome.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. <see cref="Tool"/> reflects the invoker's own
/// resolution attempt against its registered <see cref="IToolCatalog"/>; a
/// caller building a <see cref="ToolResultPart"/> from this value must use
/// <see cref="Tool"/>, not the reference it originally submitted in
/// <see cref="LegacyToolCallRequest.Tool"/>, so a successful resolution is
/// reflected in durable history.
/// </remarks>
public sealed record ResolvedToolInvocation
{
    /// <summary>Initializes a new instance of the <see cref="ResolvedToolInvocation"/> record.</summary>
    /// <param name="tool">The tool reference as resolved (or left unresolved) by the invoker.</param>
    /// <param name="projectionPolicy">The projection-policy reference captured for this invocation.</param>
    /// <param name="invocation">The terminal outcome and result content.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="tool"/>, <paramref name="projectionPolicy"/>, or <paramref name="invocation"/> is null.
    /// </exception>
    public ResolvedToolInvocation(ToolReference tool, ToolResultProjectionPolicyReference projectionPolicy, ToolInvocationResult invocation)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(projectionPolicy);
        ArgumentNullException.ThrowIfNull(invocation);

        Tool = tool;
        ProjectionPolicy = projectionPolicy;
        Invocation = invocation;
    }

    /// <summary>Gets the tool reference as resolved (or left unresolved) by the invoker.</summary>
    public ToolReference Tool { get; init; }

    /// <summary>Gets the projection-policy reference captured for this invocation.</summary>
    public ToolResultProjectionPolicyReference ProjectionPolicy { get; init; }

    /// <summary>Gets the terminal outcome and result content.</summary>
    public ToolInvocationResult Invocation { get; init; }
}
