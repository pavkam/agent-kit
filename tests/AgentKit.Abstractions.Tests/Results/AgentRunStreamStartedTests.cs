// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Results;

public sealed class AgentRunStreamStartedTests
{
    [Fact]
    public void Constructor_WhenEvidenceIsNull_RejectsExactArgument() => Should.Throw<ArgumentNullException>(() => new AgentRunStreamStarted<string>(null!)).ParamName.ShouldBe("stream");

    [Fact]
    public void Constructor_WhenStreamIsValid_RetainsOriginalStream()
    {
        var stream = new TestAgentRunStream();
        var started = new AgentRunStreamStarted<string>(stream);
        started.Stream.ShouldBeSameAs(stream);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var stream = new TestAgentRunStream();
        var original = new AgentRunStreamStarted<string>(stream);
        var copy = original with { };
        copy.Stream.ShouldBeSameAs(stream);
    }
}
