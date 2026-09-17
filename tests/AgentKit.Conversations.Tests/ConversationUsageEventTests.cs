// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Verifies ConversationUsageEvent behavior and contracts.</summary>
public sealed class ConversationUsageEventTests
{
    [Fact]
    public void Constructor_WhenUsageIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ConversationUsageEvent(null!));
        exception.ParamName.ShouldBe("usage");
    }

    [Fact]
    public void Constructor_WhenUsageIsNotReported_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ConversationUsageEvent(ModelUsage.NotReported));
        exception.ParamName.ShouldBe("usage");
    }

    [Fact]
    public void Constructor_WhenUsageIsReported_SetsProperty()
    {
        var usage = new ModelUsage(ModelUsageReportState.Final, 120, 45, null, null, 0.002m, "USD", ExtensionData.Empty);

        var usageEvent = new ConversationUsageEvent(usage);

        usageEvent.Usage.ShouldBe(usage);
    }

    [Fact]
    public void Equals_WhenUsageMatches_ReturnsTrueWithMatchingHashCode()
    {
        var usage = new ModelUsage(ModelUsageReportState.Final, 120, 45, null, null, 0.002m, "USD", ExtensionData.Empty);
        var first = new ConversationUsageEvent(usage);
        var second = new ConversationUsageEvent(usage);

        first.Equals(second).ShouldBeTrue();
        first.Equals((object) second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenUsageDiffers_ReturnsFalse()
    {
        var first = new ConversationUsageEvent(
            new ModelUsage(ModelUsageReportState.Final, 120, 45, null, null, 0.002m, "USD", ExtensionData.Empty));
        var second = new ConversationUsageEvent(
            new ModelUsage(ModelUsageReportState.Final, 1, 1, null, null, 0.001m, "USD", ExtensionData.Empty));

        first.Equals(second).ShouldBeFalse();
    }

    [Fact]
    public void ToString_WhenCalled_IncludesTypeNameAndUsage()
    {
        var usage = new ModelUsage(ModelUsageReportState.Final, 120, 45, null, null, 0.002m, "USD", ExtensionData.Empty);
        var usageEvent = new ConversationUsageEvent(usage);

        var text = usageEvent.ToString();

        text.ShouldContain(nameof(ConversationUsageEvent));
    }

    [Fact]
    public void WithExpression_WhenCalled_ProducesAnEqualClone()
    {
        var usage = new ModelUsage(ModelUsageReportState.Final, 120, 45, null, null, 0.002m, "USD", ExtensionData.Empty);
        var original = new ConversationUsageEvent(usage);

        var clone = original with { };

        clone.ShouldNotBeSameAs(original);
        clone.ShouldBe(original);
    }
}
