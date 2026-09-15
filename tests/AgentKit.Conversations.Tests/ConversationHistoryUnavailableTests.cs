// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Verifies content-safe unavailable history result construction.</summary>
public sealed class ConversationHistoryUnavailableTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsArgumentException(string? safeMessage)
    {
        var exception = Should.Throw<ArgumentException>(() => new ConversationHistoryUnavailable(safeMessage!));

        exception.ParamName.ShouldBe("safeMessage");
    }
}
