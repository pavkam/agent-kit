// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json;

/// <summary>Discriminates the authoritative state transitions appended to the approval-store record log.</summary>
/// <remarks>
/// Values start at one so a truncated or zero-filled record fails closed rather than decoding as a creation. Each
/// enumeration name is persisted as text, so adding a kind is backward compatible while renaming one is a breaking schema
/// change that must advance the store schema version. An approval has exactly two durable transitions, because a resolution
/// is terminal and is never superseded by a later record.
/// </remarks>
public enum JsonApprovalLogRecordKind
{
    /// <summary>Records one immutable approval request entering the store as pending.</summary>
    Created = 1,

    /// <summary>Records the single authenticated terminal response that resolves a pending request.</summary>
    Resolved = 2,
}
