// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.InMemory;

/// <summary>Creates collision-resistant process-local scope identities for the in-memory adapter.</summary>
internal sealed class GuidBudgetScopeIdGenerator: IIdentifierGenerator<BudgetScopeId>
{
    /// <inheritdoc/>
    public BudgetScopeId Create() => new(Guid.NewGuid());
}
