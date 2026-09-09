// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.InMemory.Tests;

/// <summary>Runs the portable budget-ledger contract against process-local storage.</summary>
public sealed class InMemoryBudgetLedgerConformanceTests: BudgetLedgerConformanceTests<InMemoryBudgetLedgerConformanceFixture>;
