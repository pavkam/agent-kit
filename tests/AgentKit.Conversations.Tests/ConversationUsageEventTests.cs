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
}
