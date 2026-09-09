// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite.ProcessHost;

/// <summary>Produces the deterministic reservation identity verified after abrupt process termination.</summary>
internal sealed class ReservationIds: IIdentifierGenerator<BudgetReservationId>
{
    /// <summary>Returns the single deterministic reservation identity used by the helper scenario.</summary>
    /// <returns>The nondefault unknown-spend reservation identity.</returns>
    public BudgetReservationId Create() => new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
}
