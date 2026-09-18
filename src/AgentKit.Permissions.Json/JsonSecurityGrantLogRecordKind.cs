// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json;

/// <summary>Discriminates the authoritative state transitions appended to the grant-store record log.</summary>
/// <remarks>
/// Values start at one so a truncated or zero-filled record fails closed rather than decoding as a registration. Each
/// enumeration name is persisted as text, so adding a kind is backward compatible while renaming one is a breaking schema
/// change that must advance the store schema version.
/// </remarks>
public enum JsonSecurityGrantLogRecordKind
{
    /// <summary>Records one immutable grant entering the store with its full allowed-use budget.</summary>
    Registered = 1,

    /// <summary>Records one consumption, carrying the new remaining-use count and any enforcement receipt in the same atomic append.</summary>
    Consumed = 2,

    /// <summary>Records that a grant is revoked and can no longer authorize an effect.</summary>
    Revoked = 3,

    /// <summary>Restates a grant's live remaining-use and revocation state, emitted only when a log is compacted.</summary>
    State = 4,

    /// <summary>Restates one enforcement-intent receipt, emitted only when a log is compacted.</summary>
    Receipt = 5,
}
