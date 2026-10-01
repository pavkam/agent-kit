// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

using static ToolRuntimeTestFixture;

/// <summary>Verifies <see cref="ToolEvent"/>.</summary>
public sealed class ToolEventTests
{
    [Fact]
    public void Constructor_WhenForeignEventIsCreated_RejectsTheUnsupportedFamily() =>
        Should.Throw<ArgumentException>(() => new ForeignToolEvent(TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId)).ParamName.ShouldBe("event");

    [Fact]
    public void CopyConstructor_WhenOriginalIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ForeignToolEvent(null!)).ParamName.ShouldBe("original");

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ToolCallAcceptedEvent(
            TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, DateTimeOffset.UnixEpoch, ToolId(), ToolVersion(), ExecutionPolicy());

        (original with { }).ShouldBe(original);
    }
}
