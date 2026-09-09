// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.InMemory;

/// <summary>Creates collision-resistant process-local reservation identities for the in-memory adapter.</summary>
internal sealed class GuidBudgetReservationIdGenerator: IIdentifierGenerator<BudgetReservationId>
{
    /// <inheritdoc/>
    public BudgetReservationId Create() => new(Guid.NewGuid());
}
