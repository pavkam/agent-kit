// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite.ProcessHost;

/// <summary>Produces the deterministic scope identity shared with the parent recovery assertion.</summary>
internal sealed class ScopeIds: IIdentifierGenerator<BudgetScopeId>
{
    /// <summary>Returns the single deterministic scope identity used by the helper scenario.</summary>
    /// <returns>The nondefault recovery fixture identity.</returns>
    public BudgetScopeId Create() => new(Guid.Parse("30000000-0000-0000-0000-000000000001"));
}
