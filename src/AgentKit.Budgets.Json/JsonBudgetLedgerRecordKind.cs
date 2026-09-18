// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json;

/// <summary>Discriminates the authoritative accounting transitions appended to the budget-ledger journal.</summary>
/// <remarks>
/// Values start at one so a truncated or zero-filled record fails closed rather than decoding as a scope creation. Each
/// name is persisted as text, so adding a kind stays backward compatible while renaming one is a breaking schema change
/// that must advance the store schema version.
/// </remarks>
public enum JsonBudgetLedgerRecordKind
{
    /// <summary>Records one admitted scope, carrying its complete original evidence and the identity the ledger allocated.</summary>
    ScopeCreated = 1,

    /// <summary>Records one indivisible batch reservation: every member's evidence, identity, and effective expiry in a single atomic append.</summary>
    BatchReserved = 2,

    /// <summary>Records one start attempt at a captured instant, which either grants start permission or expires the reservation.</summary>
    StartMarked = 3,

    /// <summary>Records release of unstarted capacity requested by the caller.</summary>
    Released = 4,

    /// <summary>Records settlement of a started reservation with its known actual usage and any resulting overrun generations.</summary>
    Settled = 5,

    /// <summary>Records one revisioned replacement of settled accounting and the hold transitions it causes.</summary>
    Corrected = 6,

    /// <summary>Records one evidence-based reconciliation of unresolved started usage, including the settlement or release it performs.</summary>
    Reconciled = 7,

    /// <summary>Records one audited operator resolution attempt and the immutable receipt bound to its replay key.</summary>
    OverrunHoldResolutionRecorded = 8,

    /// <summary>Records the lazy sweep that releases reservations already past their effective expiry at a captured instant.</summary>
    ExpirySwept = 9,
}
