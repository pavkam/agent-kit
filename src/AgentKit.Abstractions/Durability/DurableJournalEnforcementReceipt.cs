// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Authoritative evidence that one durable journal write consumed its grant and completed audit dispatch.</summary>
/// <param name="GrantId">The consumed grant identity.</param>
/// <param name="IntentId">The stable enforcement intent identity.</param>
/// <param name="AuditRecordId">The audit record identity when required audit succeeded.</param>
/// <param name="ConsumedAt">The injected-clock instant when consumption committed.</param>
public readonly record struct DurableJournalEnforcementReceipt(
    GrantId GrantId,
    SecurityEnforcementIntentId IntentId,
    SecurityAuditRecordId? AuditRecordId,
    DateTimeOffset ConsumedAt);
