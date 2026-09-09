// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports the immutable receipt for an operator-cleared hold generation.</summary>
public sealed record BudgetOverrunHoldResolved: BudgetOverrunHoldResolutionResult
{
    /// <summary>Creates a terminal receipt.</summary><param name="hold">The resolved generation.</param><param name="resolutionRevision">The accounting revision assigned to resolution.</param><param name="enforcementReceipt">The exact audit evidence persisted atomically.</param><exception cref="ArgumentNullException"><paramref name="hold"/> or <paramref name="enforcementReceipt"/> is null.</exception><exception cref="ArgumentOutOfRangeException"><paramref name="resolutionRevision"/> is default.</exception>
    public BudgetOverrunHoldResolved(BudgetOverrunHoldReference hold, BudgetAccountingRevision resolutionRevision, SecurityEnforcementIntentReceipt enforcementReceipt)
    { ArgumentNullException.ThrowIfNull(hold); ArgumentNullException.ThrowIfNull(enforcementReceipt); ArgumentOutOfRangeException.ThrowIfEqual(resolutionRevision, default); Hold = hold; ResolutionRevision = resolutionRevision; EnforcementReceipt = enforcementReceipt; }
    /// <summary>Gets the resolved generation.</summary><value>The exact historical reference.</value>
    public BudgetOverrunHoldReference Hold { get; }
    /// <summary>Gets the resolution accounting revision.</summary><value>The positive ledger-assigned revision.</value>
    public BudgetAccountingRevision ResolutionRevision { get; }
    /// <summary>Gets persisted audit evidence.</summary><value>The exact immutable enforcement receipt.</value>
    public SecurityEnforcementIntentReceipt EnforcementReceipt { get; }
}
