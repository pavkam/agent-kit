// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The requested parent branch, or the exact fork sequence within it, does
/// not exist.
/// </summary>
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
