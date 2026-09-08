// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

/// <summary>Verifies the bounded diagnostic representation of promotion-planning outcomes.</summary>
public sealed class InputPromotionPlanOutcomeExtensionsTests
{
    [Theory]
    [InlineData(0, "planned")]
    [InlineData(1, "rejected")]
    [InlineData(2, "cancelled")]
    [InlineData(3, "failed")]
    public void ToStableValue_WhenOutcomeIsDefined_ReturnsItsStableLowercaseValue(
        int outcomeValue,
        string expected)
    {
        var outcome = (InputPromotionPlanOutcome) outcomeValue;

        outcome.ToStableValue().ShouldBe(expected);
    }

    [Fact]
    public void ToStableValue_WhenOutcomeIsUndefined_ThrowsArgumentOutOfRangeExceptionWithParamName()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => ((InputPromotionPlanOutcome) 42).ToStableValue());

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("outcome");
    }
}
