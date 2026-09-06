// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A new branch was created.</summary>
public sealed record SessionBranchedEvent: SessionEvent
{
    /// <summary>Initializes a new instance of the <see cref="SessionBranchedEvent"/> record.</summary>
    /// <param name="address">The session this event concerns.</param>
    /// <param name="occurredAt">The time this event occurred.</param>
    /// <param name="parentBranchId">The branch the new branch forked from.</param>
    /// <param name="newBranchId">The newly created branch.</param>
    /// <param name="forkedAtSequence">The exact sequence the new branch forked from.</param>
    /// <exception cref="ArgumentNullException">A base parameter is null.</exception>
    public SessionBranchedEvent(
        SessionAddress address,
        DateTimeOffset occurredAt,
        BranchId parentBranchId,
        BranchId newBranchId,
        SessionSequence forkedAtSequence)
        : base(address, occurredAt)
    {
        ParentBranchId = parentBranchId;
        NewBranchId = newBranchId;
        ForkedAtSequence = forkedAtSequence;
    }

    /// <summary>Gets the branch the new branch forked from.</summary>
    public BranchId ParentBranchId { get; init; }

    /// <summary>Gets the newly created branch.</summary>
    public BranchId NewBranchId { get; init; }

    /// <summary>Gets the exact sequence the new branch forked from.</summary>
    public SessionSequence ForkedAtSequence { get; init; }
}
