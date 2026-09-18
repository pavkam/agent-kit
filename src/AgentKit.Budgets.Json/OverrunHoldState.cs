// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json;

/// <summary>Retains one boundary-specific overrun generation and any immutable operator resolution of it.</summary>
/// <remarks>
/// A generation is created by the accounting transition that crossed into overrun and is recreated deterministically when
/// that transition is replayed, so the triggering revision identifying it is stable across process loss. Instances are
/// mutated only while the owning ledger's gate is held.
/// </remarks>
internal sealed class OverrunHoldState
{
    /// <summary>Creates an active generation from its immutable creation evidence.</summary>
    /// <param name="evidence">The exact non-null generation facts.</param>
    /// <exception cref="ArgumentNullException"><paramref name="evidence"/> is null.</exception>
    internal OverrunHoldState(BudgetOverrunHold evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        Evidence = evidence;
    }

    /// <summary>Gets immutable creation evidence.</summary>
    /// <value>The exact generation facts, including the boundary's captured overrun policy.</value>
    internal BudgetOverrunHold Evidence { get; }

    /// <summary>Gets or sets whether automatic reconciliation cleared this generation.</summary>
    /// <value><see langword="true"/> only after an eligible atomic correction under the clear-when-reconciled policy.</value>
    internal bool AutomaticallyCleared { get; set; }

    /// <summary>Gets or sets the immutable operator resolution result.</summary>
    /// <value>The persisted receipt, or null while the generation remains unresolved.</value>
    internal BudgetOverrunHoldResolved? Resolution { get; set; }

    /// <summary>Gets whether this generation still blocks admission at its boundary.</summary>
    /// <value><see langword="true"/> until its policy-specific terminal transition occurs.</value>
    internal bool IsActive => !AutomaticallyCleared && Resolution is null;
}
