// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies <see cref="ToolCallRecorded"/>.</summary>
public sealed class ToolCallRecordedTests
{
    [Fact]
    public void Constructor_WhenConstructed_ReportsReplayState()
    {
        new ToolCallRecorded().Replayed.ShouldBeFalse();
        new ToolCallRecorded(replayed: true).Replayed.ShouldBeTrue();
    }
}
