// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class BudgetScopeIdTests: Conformance.GuidIdentityConformanceTests<BudgetScopeId>
{

    /// <inheritdoc/>
    protected override BudgetScopeId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(BudgetScopeId subject) => subject.Value;
}
