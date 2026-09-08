// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures an exact branch tip without using session-wide version as the branch identity.</summary>
/// <remarks>A null last entry denotes an empty branch; a present entry identifies the immutable tip observed for later revalidation.</remarks>
public sealed record SessionBranchCursor
{
    /// <summary>Initializes a branch cursor.</summary>
    /// <param name="branchId">The nondefault branch.</param>
    /// <param name="lastEntryId">The nondefault last entry, or null for an empty branch.</param>
    /// <exception cref="ArgumentOutOfRangeException">A supplied identity is default.</exception>
    public SessionBranchCursor(BranchId branchId, SessionEntryId? lastEntryId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(branchId, default);
        if (lastEntryId is { } entryId)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(entryId, default, nameof(lastEntryId));
        }

        BranchId = branchId;
        LastEntryId = lastEntryId;
    }

    /// <summary>Gets the captured branch.</summary><value>The nondefault branch identity.</value>
    public BranchId BranchId { get; }

    /// <summary>Gets the captured branch tip.</summary><value>The last entry, or null when the branch was empty.</value>
    public SessionEntryId? LastEntryId { get; }
}
