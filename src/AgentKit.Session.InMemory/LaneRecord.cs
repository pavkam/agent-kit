// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory;

/// <summary>Stores one execution lane's revision and optional complete open operation under the store gate.</summary>
internal sealed class LaneRecord
{
    /// <summary>Initializes one idle lane bound to an exact branch tip.</summary>
    /// <param name="branchCursor">The lane-owned branch and committed tip.</param><param name="revision">The positive installed lane revision.</param>
    public LaneRecord(SessionBranchCursor branchCursor, SessionLaneRevision revision)
    {
        ArgumentNullException.ThrowIfNull(branchCursor);
        ArgumentOutOfRangeException.ThrowIfEqual(revision, default);
        BranchCursor = branchCursor;
        Revision = revision;
    }

    /// <summary>Gets or sets the exact lane-owned branch tip.</summary>
    public SessionBranchCursor BranchCursor { get; set; }
    /// <summary>Gets or sets the positive lane revision.</summary>
    public SessionLaneRevision Revision { get; set; }
    /// <summary>Gets or sets complete accepted state, or null while idle.</summary>
    public SessionAcceptedRunState? AcceptedState { get; set; }
}
