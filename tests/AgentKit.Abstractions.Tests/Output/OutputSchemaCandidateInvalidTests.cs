// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;
/// <summary>Verifies OutputSchemaCandidateInvalid behavior and contracts.</summary>
public sealed class OutputSchemaCandidateInvalidTests
{
    [Fact]
    public void ClosedResults_WhenRequiredEvidenceIsNull_RejectExactParameter()
    {
        Should.Throw<ArgumentException>(() => new OutputSchemaCandidateInvalid([])).ParamName.ShouldBe("issues");
        Should.Throw<ArgumentException>(() => new OutputSchemaCandidateInvalid([null!])).ParamName.ShouldBe("issues");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsIssues()
    {
        var issue = OutputTestData.Issue();
        var invalid = new OutputSchemaCandidateInvalid([issue]);
        invalid.Issues.ShouldBe([issue]);
    }

    [Fact]
    public void Equality_WhenEquivalentArraysDifferByInstance_IsStructurallyEqual()
    {
        var left = new OutputSchemaCandidateInvalid([OutputTestData.Issue()]);
        var right = new OutputSchemaCandidateInvalid([OutputTestData.Issue()]);
        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new OutputSchemaCandidateInvalid([OutputTestData.Issue()]);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
