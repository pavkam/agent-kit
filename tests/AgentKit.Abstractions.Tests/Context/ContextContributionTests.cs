// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

/// <summary>Verifies <see cref="ContextContribution"/> invariants.</summary>
public sealed class ContextContributionTests
{
    [Fact]
    public void Constructor_WhenArraysAreValid_PreservesCandidates()
    {
        var candidate = ContextTestData.Candidate();
        var contribution = new ContextContribution([candidate], []);
        contribution.Candidates.ShouldBe([candidate]);
        contribution.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenCandidatesIsDefault_Throws() =>
        Should.Throw<ArgumentException>(() => new ContextContribution(default, [])).ParamName.ShouldBe("candidates");
}
