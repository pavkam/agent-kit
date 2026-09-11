// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class BudgetDimensionTests: Conformance.StringIdentityConformanceTests<BudgetDimension>
{

    /// <inheritdoc/>
    protected override BudgetDimension Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(BudgetDimension subject) => subject.Value;
}
