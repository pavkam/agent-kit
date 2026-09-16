// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

/// <summary>Verifies OutputValidationIssuesFound behavior and contracts.</summary>
public sealed class OutputValidationIssuesFoundTests
{
    [Fact]
    public void Constructor_WhenIssuesIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new OutputValidationIssuesFound(default)).ParamName.ShouldBe("issues");

    [Fact]
    public void Constructor_WhenIssuesIsEmpty_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new OutputValidationIssuesFound([])).ParamName.ShouldBe("issues");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsIssues()
    {
        var issue = OutputTestData.Issue();
        var found = new OutputValidationIssuesFound([issue]);
        found.Issues.ShouldBe([issue]);
    }

    [Fact]
    public void Equality_WhenEquivalentArraysDifferByInstance_IsStructurallyEqual()
    {
        var left = new OutputValidationIssuesFound([OutputTestData.Issue()]);
        var right = new OutputValidationIssuesFound([OutputTestData.Issue()]);
        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new OutputValidationIssuesFound([OutputTestData.Issue()]);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
