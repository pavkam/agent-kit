// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationCaseIdTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenValueIsBlank_ThrowsBeforeCreatingTheKey(string? value)
    {
        var exception = Should.Throw<ArgumentException>(() => new EvaluationCaseId(value!));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_WhenValueIsSupplied_PreservesTheExactText()
    {
        var key = new EvaluationCaseId("  Mixed-Case  ");

        key.Value.ShouldBe("  Mixed-Case  ");
        key.ToString().ShouldBe("  Mixed-Case  ");
    }

    [Fact]
    public void Equals_WhenTextDiffersOnlyByCase_IsNotEqual()
    {
        new EvaluationCaseId("Key").ShouldNotBe(new EvaluationCaseId("key"));
        new EvaluationCaseId("key").ShouldBe(new EvaluationCaseId("key"));
    }

    [Fact]
    public void ToString_WhenInstanceIsDefault_ReturnsEmptyText()
    {
        default(EvaluationCaseId).ToString().ShouldBeEmpty();
        default(EvaluationCaseId).Value.ShouldBeNull();
    }
}
