// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent.Tests;

public sealed class ToolchainRootValidationTests
{
    [Fact]
    public void Accepted_WhenPathIsProvided_ExposesOnlyThePath()
    {
        var outcome = ToolchainRootValidation.Accepted("/opt/homebrew");

        outcome.IsAccepted.ShouldBeTrue();
        outcome.Path.ShouldBe("/opt/homebrew");
        outcome.Error.ShouldBeNull();
    }

    [Fact]
    public void Rejected_WhenReasonIsProvided_ExposesOnlyTheError()
    {
        var outcome = ToolchainRootValidation.Rejected("nope");

        outcome.IsAccepted.ShouldBeFalse();
        outcome.Path.ShouldBeNull();
        outcome.Error.ShouldBe("nope");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Accepted_WhenPathIsBlank_Throws(string? path) =>
        Should.Throw<ArgumentException>(() => ToolchainRootValidation.Accepted(path!)).ParamName.ShouldBe("path");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Rejected_WhenErrorIsBlank_Throws(string? error) =>
        Should.Throw<ArgumentException>(() => ToolchainRootValidation.Rejected(error!)).ParamName.ShouldBe("error");
}
