// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationResultStoreKeyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenValueIsBlank_ThrowsBeforeCreatingTheKey(string? value)
    {
        var exception = Should.Throw<ArgumentException>(() => new EvaluationResultStoreKey(value!));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_WhenValueIsSupplied_PreservesTheExactText()
    {
        var key = new EvaluationResultStoreKey("  Mixed-Case  ");

        key.Value.ShouldBe("  Mixed-Case  ");
        key.ToString().ShouldBe("  Mixed-Case  ");
    }

    [Fact]
    public void Equals_WhenTextDiffersOnlyByCase_IsNotEqual()
    {
        new EvaluationResultStoreKey("Key").ShouldNotBe(new EvaluationResultStoreKey("key"));
        new EvaluationResultStoreKey("key").ShouldBe(new EvaluationResultStoreKey("key"));
    }

    [Fact]
    public void ToString_WhenInstanceIsDefault_ReturnsEmptyText()
    {
        default(EvaluationResultStoreKey).ToString().ShouldBeEmpty();
        default(EvaluationResultStoreKey).Value.ShouldBeNull();
    }
}
