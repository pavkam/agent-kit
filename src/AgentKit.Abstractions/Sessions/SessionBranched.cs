// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The new branch was created (or an identical prior request with the same
/// idempotency key already created it), leaving the parent branch
/// untouched.
/// </summary>
public sealed record SessionBranched: SessionBranchResult
{
    /// <summary>Initializes a new instance of the <see cref="SessionBranched"/> record.</summary>
    /// <param name="newBranchId">The newly created branch's identity.</param>
    /// <param name="forkedAtSequence">The exact sequence the new branch forked from.</param>
    public SessionBranched(BranchId newBranchId, SessionSequence forkedAtSequence)
    {
        NewBranchId = newBranchId;
        ForkedAtSequence = forkedAtSequence;
    }

    /// <summary>Gets the newly created branch's identity.</summary>
    public BranchId NewBranchId { get; init; }

    /// <summary>Gets the exact sequence the new branch forked from.</summary>
    public SessionSequence ForkedAtSequence { get; init; }
}
