// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries one exact authorized current-plan observation.</summary>
public sealed record PlanReadRequest
{
    /// <summary>Initializes a protected plan read.</summary>
    /// <param name="context">The target session and authenticated operation context.</param>
    /// <param name="sessionProfile">The exact immutable session profile for coordinator access.</param>
    /// <param name="toolCallId">The causing tool call.</param>
    /// <param name="grant">The exact grant the store must consume.</param>
    /// <exception cref="ArgumentNullException">A reference is null.</exception>
    public PlanReadRequest(SessionOperationContext context, SessionProfileSnapshot sessionProfile,
        ToolCallId toolCallId, SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sessionProfile);
        ArgumentNullException.ThrowIfNull(grant);
        Context = context;
        SessionProfile = sessionProfile;
        ToolCallId = toolCallId;
        Grant = grant;
    }

    /// <summary>Gets the target session and operation context.</summary>
    public SessionOperationContext Context { get; }
    /// <summary>Gets the immutable session profile used for coordinator access.</summary>
    public SessionProfileSnapshot SessionProfile { get; }
    /// <summary>Gets the causing tool call.</summary>
    public ToolCallId ToolCallId { get; }
    /// <summary>Gets the exact observation grant.</summary>
    public SecurityGrant Grant { get; }
}
