// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite;

/// <summary>Identifies one closed payload shape in the version-one budget evidence codec.</summary>
internal enum BudgetEvidenceKind: byte
{
    ScopeCreateRequest = 1,
    BatchReserveRequest = 2,
    BatchReserved = 3,
    ReservationReceipt = 4,
    StartExpired = 5,
    CommitResult = 6,
    OverrunHold = 7,
    CorrectionResult = 8,
    ReconciliationEvidence = 9,
    ReconciliationResult = 10,
    OverrunResolutionRequest = 11,
    OverrunResolutionResult = 12,
}
