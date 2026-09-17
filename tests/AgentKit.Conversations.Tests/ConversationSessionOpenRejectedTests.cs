// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Verifies content-safe session-open rejection construction.</summary>
public sealed class ConversationSessionOpenRejectedTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsArgumentException(string? safeMessage)
    {
        var exception = Should.Throw<ArgumentException>(() => new ConversationSessionOpenRejected(safeMessage!));

        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void Constructor_WhenSafeMessageIsNonBlank_SetsSafeMessage()
    {
        var result = new ConversationSessionOpenRejected("open rejected");

        result.SafeMessage.ShouldBe("open rejected");
    }

    [Fact]
    public void Equals_WhenSafeMessageMatches_ReturnsTrueWithMatchingHashCode()
    {
        var first = new ConversationSessionOpenRejected("open rejected");
        var second = new ConversationSessionOpenRejected("open rejected");

        first.Equals(second).ShouldBeTrue();
        first.Equals((object) second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenSafeMessageDiffers_ReturnsFalse()
    {
        var first = new ConversationSessionOpenRejected("open rejected");
        var second = new ConversationSessionOpenRejected("a different reason");

        first.Equals(second).ShouldBeFalse();
    }

    [Fact]
    public void ToString_WhenCalled_IncludesTypeNameAndSafeMessage()
    {
        var result = new ConversationSessionOpenRejected("open rejected");

        var text = result.ToString();

        text.ShouldContain(nameof(ConversationSessionOpenRejected));
        text.ShouldContain("open rejected");
    }

    [Fact]
    public void WithExpression_WhenCalled_ProducesAnEqualClone()
    {
        var original = new ConversationSessionOpenRejected("open rejected");

        var clone = original with { };

        clone.ShouldNotBeSameAs(original);
        clone.ShouldBe(original);
    }
}
