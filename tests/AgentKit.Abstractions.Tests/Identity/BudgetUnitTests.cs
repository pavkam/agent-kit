// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class BudgetUnitTests: Conformance.StringIdentityConformanceTests<BudgetUnit>
{

    /// <inheritdoc/>
    protected override BudgetUnit Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(BudgetUnit subject) => subject.Value;
}
