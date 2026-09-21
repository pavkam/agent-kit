// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

/// <summary>Verifies <see cref="ContextMessageTokenEstimation"/> behavior.</summary>
public sealed class ContextMessageTokenEstimationTests
{
    [Fact]
    public void EstimateTokens_WhenMessageContainsText_EstimatesFromCharacterLength()
    {
        var message = new AgentMessage(
            new MessageId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            MessageRole.User,
            [new TextPart("12345678")],
            ExtensionData.Empty);
        ContextMessageTokenEstimation.EstimateTokens([message], estimatedCharactersPerToken: 4).ShouldBe(2);
    }

    [Fact]
    public void EstimateTokens_WhenCharactersPerTokenIsNotPositive_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            ContextMessageTokenEstimation.EstimateTokens([], estimatedCharactersPerToken: 0)).ParamName.ShouldBe("estimatedCharactersPerToken");
}
