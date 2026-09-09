// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares immutable durability and concurrency guarantees for one budget ledger implementation.</summary>
/// <remarks>Reading this value is side-effect free and never opens, probes, initializes, or migrates storage.</remarks>
public sealed record BudgetLedgerDescriptor
{
    /// <summary>Initializes one declared ledger capability profile.</summary>
    /// <param name="durable">Whether committed ledger state survives the implementation's documented process lifetime boundary.</param>
    /// <param name="concurrencyDomain">The defined ownership domain across which mandatory ledger transitions linearize.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="concurrencyDomain"/> is undefined.</exception>
    public BudgetLedgerDescriptor(bool durable, BudgetLedgerConcurrencyDomain concurrencyDomain)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(concurrencyDomain);
        Durable = durable;
        ConcurrencyDomain = concurrencyDomain;
    }

    /// <summary>Gets whether committed state survives the implementation's documented process lifetime boundary.</summary>
    /// <value><see langword="true"/> only when restart persistence is part of the adapter's tested guarantee.</value>
    public bool Durable { get; }

    /// <summary>Gets the ownership domain across which the ledger provides mandatory atomic accounting.</summary>
    /// <value>A defined domain; <see cref="BudgetLedgerConcurrencyDomain.Distributed"/> includes authoritative fencing.</value>
    public BudgetLedgerConcurrencyDomain ConcurrencyDomain { get; }
}
