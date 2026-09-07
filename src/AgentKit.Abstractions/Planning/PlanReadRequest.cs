// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries one exact authorized current-plan observation.</summary>
public sealed record PlanReadRequest
{
    /// <summary>Initializes a protected plan read.</summary>
    /// <param name="context">The target session and authenticated operation context.</param>
    /// <param name="toolCallId">The causing tool call.</param>
    /// <param name="grant">The exact grant the store must consume.</param>
    /// <exception cref="ArgumentNullException">A reference is null.</exception>
    public PlanReadRequest(SessionOperationContext context, ToolCallId toolCallId, SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(grant);
        Context = context;
        ToolCallId = toolCallId;
        Grant = grant;
    }

    /// <summary>Gets the target session and operation context.</summary>
    public SessionOperationContext Context { get; init; }
    /// <summary>Gets the causing tool call.</summary>
    public ToolCallId ToolCallId { get; init; }
    /// <summary>Gets the exact observation grant.</summary>
    public SecurityGrant Grant { get; init; }
}
