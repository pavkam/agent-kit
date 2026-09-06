// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Entries were appended to a session branch.</summary>
public sealed record SessionAppendedEvent: SessionEvent
{
    /// <summary>Initializes a new instance of the <see cref="SessionAppendedEvent"/> record.</summary>
    /// <param name="address">The session this event concerns.</param>
    /// <param name="occurredAt">The time this event occurred.</param>
    /// <param name="branchId">The branch entries were appended to.</param>
    /// <param name="newVersion">The branch's new version after this append.</param>
    /// <param name="entryCount">The number of entries appended.</param>
    /// <exception cref="ArgumentNullException">A base parameter is null.</exception>
    public SessionAppendedEvent(
        SessionAddress address,
        DateTimeOffset occurredAt,
        BranchId branchId,
        SessionVersion newVersion,
        int entryCount)
        : base(address, occurredAt)
    {
        BranchId = branchId;
        NewVersion = newVersion;
        EntryCount = entryCount;
    }

    /// <summary>Gets the branch entries were appended to.</summary>
    public BranchId BranchId { get; init; }

    /// <summary>Gets the branch's new version after this append.</summary>
    public SessionVersion NewVersion { get; init; }

    /// <summary>Gets the number of entries appended.</summary>
    public int EntryCount { get; init; }
}
