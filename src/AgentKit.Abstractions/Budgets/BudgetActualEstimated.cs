// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Supplies an explicitly estimated actual usage value during reconciliation.</summary>
public sealed record BudgetActualEstimated: BudgetReconciliationEvidence
{
    /// <summary>Initializes estimated usage evidence.</summary><param name="actual">The nonnegative conservative estimate.</param><exception cref="ArgumentOutOfRangeException"><paramref name="actual"/> is negative.</exception>
    public BudgetActualEstimated(decimal actual) { ArgumentOutOfRangeException.ThrowIfNegative(actual); Actual = actual; }
    /// <summary>Gets estimated actual usage.</summary><value>A nonnegative quantity in the reservation's unit.</value>
    public decimal Actual { get; }
}
