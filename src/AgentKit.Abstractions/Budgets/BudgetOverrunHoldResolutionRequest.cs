// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests one audited, idempotent operator resolution of an exact persisted hold generation.</summary>
public sealed record BudgetOverrunHoldResolutionRequest
{
    /// <summary>Creates a resolution request.</summary><param name="hold">The exact generation.</param><param name="enforcementReceipt">Consumed security-enforcement evidence retained for audit, not proof of authenticity to the ledger.</param><param name="idempotencyKey">The nonblank replay key.</param><exception cref="ArgumentNullException"><paramref name="hold"/> or <paramref name="enforcementReceipt"/> is null.</exception><exception cref="ArgumentException"><paramref name="idempotencyKey"/> is blank.</exception>
    public BudgetOverrunHoldResolutionRequest(BudgetOverrunHoldReference hold, SecurityEnforcementIntentReceipt enforcementReceipt, IdempotencyKey idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(hold); ArgumentNullException.ThrowIfNull(enforcementReceipt); ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        Hold = hold; EnforcementReceipt = enforcementReceipt; IdempotencyKey = idempotencyKey;
    }
    /// <summary>Gets the exact hold generation.</summary><value>The composite persisted locator.</value>
    public BudgetOverrunHoldReference Hold { get; }
    /// <summary>Gets audit evidence supplied by the enforcing runtime.</summary><value>The immutable consumed-intent receipt; the ledger does not authenticate it.</value>
    public SecurityEnforcementIntentReceipt EnforcementReceipt { get; }
    /// <summary>Gets the replay key.</summary><value>A nonblank stable key.</value>
    public IdempotencyKey IdempotencyKey { get; }
}
