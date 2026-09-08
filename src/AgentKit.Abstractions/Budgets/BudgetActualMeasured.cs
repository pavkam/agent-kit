// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Supplies a measured actual usage value during reconciliation.</summary>
public sealed record BudgetActualMeasured: BudgetReconciliationEvidence
{
    /// <summary>Initializes measured usage evidence.</summary><param name="actual">The nonnegative actual usage measured by an authoritative source.</param><exception cref="ArgumentOutOfRangeException"><paramref name="actual"/> is negative.</exception>
    public BudgetActualMeasured(decimal actual) { ArgumentOutOfRangeException.ThrowIfNegative(actual); Actual = actual; }
    /// <summary>Gets measured actual usage.</summary><value>A nonnegative quantity in the reservation's unit.</value>
    public decimal Actual { get; }
}
