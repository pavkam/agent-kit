// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationPlanIdTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenValueIsBlank_ThrowsBeforeCreatingTheKey(string? value)
    {
        var exception = Should.Throw<ArgumentException>(() => new EvaluationPlanId(value!));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_WhenValueIsSupplied_PreservesTheExactText()
    {
        var key = new EvaluationPlanId("  Mixed-Case  ");

        key.Value.ShouldBe("  Mixed-Case  ");
        key.ToString().ShouldBe("  Mixed-Case  ");
    }

    [Fact]
    public void Equals_WhenTextDiffersOnlyByCase_IsNotEqual()
    {
        new EvaluationPlanId("Key").ShouldNotBe(new EvaluationPlanId("key"));
        new EvaluationPlanId("key").ShouldBe(new EvaluationPlanId("key"));
    }

    [Fact]
    public void ToString_WhenInstanceIsDefault_ReturnsEmptyText()
    {
        default(EvaluationPlanId).ToString().ShouldBeEmpty();
        default(EvaluationPlanId).Value.ShouldBeNull();
    }
}
