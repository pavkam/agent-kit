// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The requested parent branch, or the exact fork sequence within it, does
/// not exist.
/// </summary>
/// <remarks>
/// Stores return this result when the parent branch is unavailable to the caller
/// and when a nonzero <see cref="AtSequence"/> is not the sequence of an entry
/// committed on that branch, including sequences that the session allocated to a
/// sibling branch.
/// </remarks>
public sealed record SessionBranchParentNotFound: SessionBranchResult
{
    /// <summary>Initializes a new instance of the <see cref="SessionBranchParentNotFound"/> record.</summary>
    /// <param name="parentBranchId">The requested parent branch.</param>
    /// <param name="atSequence">The requested fork sequence.</param>
    public SessionBranchParentNotFound(BranchId parentBranchId, SessionSequence atSequence)
    {
        ParentBranchId = parentBranchId;
        AtSequence = atSequence;
    }

    /// <summary>Gets the requested parent branch.</summary>
    public BranchId ParentBranchId { get; init; }

    /// <summary>Gets the requested fork sequence.</summary>
    public SessionSequence AtSequence { get; init; }
}
