// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares the ownership domain across which a budget ledger linearizes its mandatory atomic contract.</summary>
public enum BudgetLedgerConcurrencyDomain
{
    /// <summary>Coordinates callers only inside one process and makes no cross-process claim.</summary>
    ProcessLocal,
    /// <summary>Coordinates callers sharing one host-local ledger domain, including supported processes.</summary>
    HostLocal,
    /// <summary>Coordinates distributed callers with the authoritative fencing and atomicity required by the budget contract.</summary>
    Distributed,
}
