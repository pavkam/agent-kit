// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

public sealed class ModelSelectionPolicyTests
{
    [Fact]
    public void Constructor_WhenCandidatesContainDefaultAlias_ThrowsArgumentOutOfRangeWithCandidates() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ModelSelectionPolicy([default])).ParamName.ShouldBe("candidates");

    [Fact]
    public void CandidatesInit_WhenCandidatesContainDefaultAlias_ThrowsArgumentOutOfRangeWithCandidates()
    {
        var policy = new ModelSelectionPolicy([new ModelAlias("chat")]);
        Should.Throw<ArgumentOutOfRangeException>(() => policy with { Candidates = [new ModelAlias("chat"), default] }).ParamName.ShouldBe("Candidates");
    }

    [Fact]
    public void Constructor_WhenCandidatesValid_PreservesCandidateOrder()
    {
        var policy = new ModelSelectionPolicy([new ModelAlias("a"), new ModelAlias("b")]);
        policy.Candidates.ShouldBe([new ModelAlias("a"), new ModelAlias("b")]);
    }
}
