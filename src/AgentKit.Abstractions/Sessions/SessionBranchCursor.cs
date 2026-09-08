// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures an exact selected-branch tip without treating a session-wide version as branch identity.</summary>
/// <remarks>The cursor is immutable revalidation evidence. A null last entry denotes an empty branch; a present entry identifies the observed immutable tip. It neither grants branch mutation authority nor proves that a later compare-and-swap will succeed.</remarks>
public sealed record SessionBranchCursor
{
    /// <summary>Initializes an observed branch-tip cursor.</summary>
    /// <param name="branchId">The non-default identity of the selected branch.</param>
    /// <param name="lastEntryId">The non-default identity of its observed last entry, or <see langword="null"/> when the branch was empty.</param>
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

    /// <summary>Gets the identity of the selected branch.</summary>
    /// <value>A non-default branch identity; it scopes <see cref="LastEntryId"/> and cannot be replaced by a session version.</value>
    public BranchId BranchId { get; }

    /// <summary>Gets the observed immutable tip of the selected branch.</summary>
    /// <value>A non-default session entry identity, or <see langword="null"/> when the branch was empty at observation time.</value>
    public SessionEntryId? LastEntryId { get; }
}
