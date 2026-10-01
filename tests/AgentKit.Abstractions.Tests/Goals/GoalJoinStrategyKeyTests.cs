// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

/// <summary>Verifies GoalJoinStrategyKey behavior and contracts.</summary>
public sealed class GoalJoinStrategyKeyTests: Conformance.StringIdentityConformanceTests<GoalJoinStrategyKey>
{
    /// <inheritdoc/>
    protected override GoalJoinStrategyKey Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(GoalJoinStrategyKey subject) => subject.Value;

    [Fact]
    public void Equality_WhenSameTextAsAnotherKeyType_TypesRemainDistinct() =>
        ((object) new GoalJoinStrategyKey("shared")).ShouldNotBe(new GoalStoreKey("shared"));
}
