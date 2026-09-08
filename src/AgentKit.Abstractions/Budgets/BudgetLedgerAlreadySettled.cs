// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that release made no transition because the reservation was already settled.</summary>
public sealed record BudgetLedgerAlreadySettled: BudgetLedgerReleaseResult
{
    /// <summary>Initializes the settled no-op receipt.</summary>
    /// <param name="commit">The non-null persisted settlement that makes release unnecessary.</param>
    /// <exception cref="ArgumentNullException"><paramref name="commit"/> is null.</exception>
    public BudgetLedgerAlreadySettled(BudgetCommitResult commit)
    {
        ArgumentNullException.ThrowIfNull(commit);
        Commit = commit;
    }
    /// <summary>Gets the settlement already recorded for this reservation.</summary>
    /// <value>Never null.</value>
    public BudgetCommitResult Commit { get; }
}
