// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a started reservation remains retained because its spend is not yet known.</summary>
public sealed record BudgetLedgerRetainedStarted: BudgetLedgerReleaseResult
{
    /// <summary>Initializes a retained-started receipt.</summary><param name="reservation">The non-null exact reservation retained for reconciliation.</param><exception cref="ArgumentNullException"><paramref name="reservation"/> is null.</exception>
    public BudgetLedgerRetainedStarted(BudgetLedgerReservationReference reservation) { ArgumentNullException.ThrowIfNull(reservation); Reservation = reservation; }
    /// <summary>Gets the retained reservation locator.</summary><value>Never null.</value>
    public BudgetLedgerReservationReference Reservation { get; }
}
