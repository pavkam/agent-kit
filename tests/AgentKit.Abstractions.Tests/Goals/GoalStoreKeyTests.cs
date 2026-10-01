// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

/// <summary>Verifies GoalStoreKey behavior and contracts.</summary>
public sealed class GoalStoreKeyTests: Conformance.StringIdentityConformanceTests<GoalStoreKey>
{
    /// <inheritdoc/>
    protected override GoalStoreKey Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(GoalStoreKey subject) => subject.Value;

    [Fact]
    public void Equality_WhenSameTextAsAnotherKeyType_TypesRemainDistinct() =>
        ((object) new GoalStoreKey("shared")).ShouldNotBe(new DelegationDispatcherKey("shared"));
}
