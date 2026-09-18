// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json;

/// <summary>Discriminates the closed <see cref="BudgetReconciliationEvidence"/> hierarchy inside one persisted journal record.</summary>
/// <remarks>
/// Values start at one so a truncated or zero-filled record fails closed rather than decoding as measured usage. Each name
/// is persisted as text, so adding a kind stays backward compatible while renaming one is a breaking schema change that
/// must advance the store schema version. The distinction between measured and estimated usage is preserved because the
/// budget contract treats provenance as part of the accounting fact, not as a presentation detail.
/// </remarks>
public enum JsonBudgetReconciliationEvidenceKind
{
    /// <summary>Records that an authoritative source measured the actual usage now carried by the record.</summary>
    Measured = 1,

    /// <summary>Records that the caller supplied an explicitly conservative estimate rather than a measurement.</summary>
    Estimated = 2,

    /// <summary>Records durable external proof that the started effect consumed no budgeted usage.</summary>
    NoUsageProven = 3,

    /// <summary>Records that reconciliation produced no evidence, so the started capacity stays charged.</summary>
    StillUnknown = 4,
}
