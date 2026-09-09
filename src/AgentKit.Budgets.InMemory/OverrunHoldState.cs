// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.InMemory;

/// <summary>Retains one boundary-specific overrun generation and any immutable operator resolution.</summary>
internal sealed class OverrunHoldState
{
    /// <summary>Creates an active generation.</summary><param name="evidence">The immutable creation evidence.</param><exception cref="ArgumentNullException"><paramref name="evidence"/> is null.</exception>
    internal OverrunHoldState(BudgetOverrunHold evidence) { ArgumentNullException.ThrowIfNull(evidence); Evidence = evidence; }
    /// <summary>Gets immutable creation evidence.</summary><value>The exact generation facts.</value>
    internal BudgetOverrunHold Evidence { get; }
    /// <summary>Gets or sets whether automatic reconciliation cleared this generation.</summary><value>True only after an eligible atomic correction.</value>
    internal bool AutomaticallyCleared { get; set; }
    /// <summary>Gets or sets the immutable operator result.</summary><value>The persisted receipt, or null while unresolved.</value>
    internal BudgetOverrunHoldResolved? Resolution { get; set; }
    /// <summary>Gets whether this generation still blocks admission.</summary><value>True until its policy-specific terminal transition.</value>
    internal bool IsActive => !AutomaticallyCleared && Resolution is null;
}
