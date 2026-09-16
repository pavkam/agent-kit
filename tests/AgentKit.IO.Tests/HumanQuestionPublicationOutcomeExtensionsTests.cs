// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

/// <summary>Verifies the bounded diagnostic representation of human-question publication outcomes.</summary>
public sealed class HumanQuestionPublicationOutcomeExtensionsTests
{
    [Theory]
    [InlineData(0, "answered")]
    [InlineData(1, "timed_out")]
    [InlineData(2, "channel_unavailable")]
    [InlineData(3, "grant_denied")]
    [InlineData(4, "authorization_mismatch")]
    [InlineData(5, "cancelled")]
    [InlineData(6, "failed")]
    public void ToStableValue_WhenOutcomeIsDefined_ReturnsItsStableLowercaseValue(
        int outcomeValue,
        string expected)
    {
        var outcome = (HumanQuestionPublicationOutcome) outcomeValue;

        outcome.ToStableValue().ShouldBe(expected);
    }

    [Fact]
    public void ToStableValue_WhenOutcomeIsUndefined_ThrowsArgumentOutOfRangeExceptionWithParamName()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => ((HumanQuestionPublicationOutcome) 42).ToStableValue());

        exception.ParamName.ShouldBe("outcome");
    }
}
