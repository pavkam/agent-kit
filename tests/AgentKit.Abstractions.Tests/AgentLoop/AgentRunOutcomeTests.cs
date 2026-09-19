// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.AgentLoop;

/// <summary>Verifies AgentRunOutcome behavior and contracts.</summary>
public sealed class AgentRunOutcomeTests
{
    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new RunIdle();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
