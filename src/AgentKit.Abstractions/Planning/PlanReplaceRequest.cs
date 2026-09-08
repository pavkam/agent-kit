// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries one exact authorized replacement of the current plan.</summary>
public sealed record PlanReplaceRequest
{
    /// <summary>Initializes a protected plan replacement.</summary>
    /// <param name="context">The target session and authenticated operation context.</param>
    /// <param name="sessionProfile">The exact immutable session profile for coordinator access.</param>
    /// <param name="toolCallId">The causing tool call.</param>
    /// <param name="title">The new plan title.</param>
    /// <param name="items">The new ordered item snapshot.</param>
    /// <param name="expectedRevision">The expected current revision, or null when no plan is expected.</param>
    /// <param name="grant">The exact grant the store must consume.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentException">The title or item collection is invalid.</exception>
    public PlanReplaceRequest(
        SessionOperationContext context,
        SessionProfileSnapshot sessionProfile,
        ToolCallId toolCallId,
        string title,
        ImmutableArray<WorkPlanItem> items,
        PlanRevision? expectedRevision,
        SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sessionProfile);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfContainsNull(items);
        ArgumentException.ThrowIfDefaultOrEmpty(items);
        ArgumentException.ThrowIfDuplicatePlanItemIds(items);
        ArgumentException.ThrowIfMultipleInProgressPlanItems(items);
        ArgumentNullException.ThrowIfNull(grant);
        Context = context;
        SessionProfile = sessionProfile;
        ToolCallId = toolCallId;
        Title = title;
        Items = items;
        ExpectedRevision = expectedRevision;
        Grant = grant;
    }

    /// <summary>Gets the target session and operation context.</summary>
    public SessionOperationContext Context { get; }
    /// <summary>Gets the immutable session profile used for coordinator access.</summary>
    public SessionProfileSnapshot SessionProfile { get; }
    /// <summary>Gets the causing tool call.</summary>
    public ToolCallId ToolCallId { get; }
    /// <summary>Gets the new plan title.</summary>
    public string Title { get; }
    /// <summary>Gets the new ordered item snapshot.</summary>
    public ImmutableArray<WorkPlanItem> Items { get; }
    /// <summary>Gets the expected current revision, or null when no plan is expected.</summary>
    public PlanRevision? ExpectedRevision { get; }
    /// <summary>Gets the exact mutation grant.</summary>
    public SecurityGrant Grant { get; }
}
