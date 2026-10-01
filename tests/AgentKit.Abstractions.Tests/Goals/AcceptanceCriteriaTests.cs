// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

/// <summary>Verifies AcceptanceCriteria constraints and value semantics.</summary>
public sealed class AcceptanceCriteriaTests
{
    [Fact]
    public void Constructor_WhenCriteriaAreDefault_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new AcceptanceCriteria(default, false)).ParamName.ShouldBe("criteria");

    [Fact]
    public void Constructor_WhenACriterionIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new AcceptanceCriteria(["ok", ""], false)).ParamName.ShouldBe("criteria");

    [Fact]
    public void Constructor_WhenCriteriaAreEmpty_IsAllowed() => new AcceptanceCriteria([], true).RequiresEvidence.ShouldBeTrue();

    [Fact]
    public void Equality_WhenCriteriaMatchByContent_IsStructural() =>
        new AcceptanceCriteria(["a"], true).ShouldBe(new AcceptanceCriteria(["a"], true));
}
