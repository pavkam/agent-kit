// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>States that reconciliation produced no evidence sufficient to release or settle the started capacity.</summary>
public sealed record BudgetStillUnknown: BudgetReconciliationEvidence;
