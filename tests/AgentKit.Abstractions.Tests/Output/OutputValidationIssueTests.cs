// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

/// <summary>Verifies OutputValidationIssue behavior and contracts.</summary>
public sealed class OutputValidationIssueTests
{
    [Fact]
    public void Constructor_WhenCodeIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new OutputValidationIssue(" ", "message", null)).ParamName.ShouldBe("code");

    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new OutputValidationIssue("code", " ", null)).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var issue = new OutputValidationIssue("code", "message", "$.path");
        issue.Code.ShouldBe("code");
        issue.SafeMessage.ShouldBe("message");
        issue.Path.ShouldBe("$.path");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new OutputValidationIssue("code", "message", "$.path");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
