// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

/// <summary>Verifies <see cref="ContextMessageTokenEstimation"/> behavior.</summary>
public sealed class ContextMessageTokenEstimationTests
{
    [Fact]
    public void EstimateTokens_WhenMessageContainsText_EstimatesFromCharacterLength()
    {
        var message = new UserMessage(
            new MessageId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            new AgentId(Guid.Parse("20000000-0000-0000-0000-000000000001")),
            new SessionId(Guid.Parse("30000000-0000-0000-0000-000000000001")),
            null,
            new BranchId(Guid.Parse("40000000-0000-0000-0000-000000000001")),
            null,
            null,
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            [new TextPart("12345678", TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);
        ContextMessageTokenEstimation.EstimateTokens(
            ImmutableArray.Create(message),
            estimatedCharactersPerToken: 4).ShouldBe(2);
    }

    [Fact]
    public void EstimateTokens_WhenCharactersPerTokenIsNotPositive_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            ContextMessageTokenEstimation.EstimateTokens(
                ImmutableArray<AgentMessage>.Empty,
                estimatedCharactersPerToken: 0)).ParamName.ShouldBe("estimatedCharactersPerToken");
}
