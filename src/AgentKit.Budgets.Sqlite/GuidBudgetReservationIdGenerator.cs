// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite;

/// <summary>Creates process-random reservation identities for hosts that do not replace the generator.</summary>
internal sealed class GuidBudgetReservationIdGenerator: IIdentifierGenerator<BudgetReservationId>
{
    /// <inheritdoc/>
    public BudgetReservationId Create() => new(Guid.NewGuid());
}
