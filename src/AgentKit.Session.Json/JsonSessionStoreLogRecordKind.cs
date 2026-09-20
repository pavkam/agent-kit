// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

/// <summary>Discriminates the authoritative session transitions appended to the store record log.</summary>
/// <remarks>
/// Values start at one so a truncated or zero-filled record fails closed rather than decoding as a session creation. Each
/// enumeration name is persisted as text, so adding a kind is backward compatible while renaming one is a breaking schema
/// change that must advance the store schema version.
/// </remarks>
public enum JsonSessionStoreLogRecordKind
{
    /// <summary>Records one session entering the store with its allocated address and initial branch.</summary>
    SessionCreated = 1,

    /// <summary>Records one execution lane claiming a previously unowned branch tip.</summary>
    LaneProvisioned = 2,

    /// <summary>Records one accepted conditional append of a contiguous entry batch.</summary>
    EntriesAppended = 3,

    /// <summary>Records one branch forked from a committed parent point.</summary>
    BranchCreated = 4,

    /// <summary>Records one session deletion and the retry evidence it retires.</summary>
    SessionDeleted = 5,

    /// <summary>Records one accepted input admission or the reconciliation of an equivalent prior admission.</summary>
    InputAdmitted = 6,

    /// <summary>Records one atomic run acceptance that promotes selected admissions and installs accepted state.</summary>
    RunAccepted = 7,

    /// <summary>Records one release of a lane's installed accepted run.</summary>
    RunReleased = 8,

    /// <summary>Records one atomic mid-run promotion of durably admitted input into an already-accepted run's turn.</summary>
    InputPromoted = 9,

    /// <summary>Records one durable abort: cancel marker, pending-admission prune, and revision advance.</summary>
    RunAborted = 10,
}
