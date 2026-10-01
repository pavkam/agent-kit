// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

/// <summary>Verifies DelegationDispatcherKey behavior and contracts.</summary>
public sealed class DelegationDispatcherKeyTests: Conformance.StringIdentityConformanceTests<DelegationDispatcherKey>
{
    /// <inheritdoc/>
    protected override DelegationDispatcherKey Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(DelegationDispatcherKey subject) => subject.Value;

    [Fact]
    public void Equality_WhenSameTextAsAnotherKeyType_TypesRemainDistinct() =>
        ((object) new DelegationDispatcherKey("shared")).ShouldNotBe(new GoalJoinStrategyKey("shared"));
}
