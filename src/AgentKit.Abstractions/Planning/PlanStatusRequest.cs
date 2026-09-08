// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries one exact authorized optimistic item-status transition.</summary>
public sealed record PlanStatusRequest
{
    /// <summary>Initializes a protected item-status transition.</summary>
    /// <param name="context">The target session and authenticated operation context.</param>
    /// <param name="sessionProfile">The exact immutable session profile for coordinator access.</param>
    /// <param name="toolCallId">The causing tool call.</param>
    /// <param name="itemId">The item to update.</param>
    /// <param name="status">The requested status.</param>
    /// <param name="expectedRevision">The expected current revision.</param>
    /// <param name="grant">The exact grant the store must consume.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is undefined.</exception>
    public PlanStatusRequest(
        SessionOperationContext context,
        SessionProfileSnapshot sessionProfile,
        ToolCallId toolCallId,
        PlanItemId itemId,
        PlanItemStatus status,
        PlanRevision expectedRevision,
        SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sessionProfile);
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        ArgumentNullException.ThrowIfNull(grant);
        Context = context;
        SessionProfile = sessionProfile;
        ToolCallId = toolCallId;
        ItemId = itemId;
        Status = status;
        ExpectedRevision = expectedRevision;
        Grant = grant;
    }

    /// <summary>Gets the target session and operation context.</summary>
    public SessionOperationContext Context { get; }
    /// <summary>Gets the immutable session profile used for coordinator access.</summary>
    public SessionProfileSnapshot SessionProfile { get; }
    /// <summary>Gets the causing tool call.</summary>
    public ToolCallId ToolCallId { get; }
    /// <summary>Gets the item to update.</summary>
    public PlanItemId ItemId { get; }
    /// <summary>Gets the requested status.</summary>
    public PlanItemStatus Status { get; }
    /// <summary>Gets the expected current revision.</summary>
    public PlanRevision ExpectedRevision { get; }
    /// <summary>Gets the exact mutation grant.</summary>
    public SecurityGrant Grant { get; }
}
