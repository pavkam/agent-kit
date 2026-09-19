// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using System.Text.Json;

using AgentKit;

using static ToolRuntimeTestFixture;

/// <summary>Verifies ToolInvocationContext behavior and contracts.</summary>
public sealed class ToolInvocationContextTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var tool = Descriptor();
        var grant = Grant();
        var progress = Progress();
        var arguments = JsonDocument.Parse("""{"a":1}""").RootElement;
        var context = new ToolInvocationContext(
            TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, tool, tool.Version, arguments, grant,
            2, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddSeconds(1), DateTimeOffset.UnixEpoch.AddMinutes(1), progress);

        context.AgentId.ShouldBe(TestAgentId);
        context.SessionId.ShouldBe(TestSessionId);
        context.RunId.ShouldBe(TestRunId);
        context.TurnId.ShouldBe(TestTurnId);
        context.OperationId.ShouldBe(TestOperationId);
        context.CallId.ShouldBe(CallId);
        context.Tool.ShouldBe(tool);
        context.ToolVersion.ShouldBe(tool.Version);
        context.Arguments.GetRawText().ShouldBe(arguments.GetRawText());
        context.InvocationGrant.ShouldBe(grant);
        context.Attempt.ShouldBe(2);
        context.RequestedAt.ShouldBe(DateTimeOffset.UnixEpoch);
        context.InvocationStartedAt.ShouldBe(DateTimeOffset.UnixEpoch.AddSeconds(1));
        context.Deadline.ShouldBe(DateTimeOffset.UnixEpoch.AddMinutes(1));
        context.Progress.ShouldBeSameAs(progress);
    }

    [Fact]
    public void Constructor_WhenToolIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolInvocationContext(
            TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, null!, ToolVersion(),
            default, Grant(), 1, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddMinutes(1), Progress())).ParamName.ShouldBe("tool");

    [Fact]
    public void Constructor_WhenToolVersionDoesNotMatchDescriptor_ThrowsExactParameter()
    {
        var tool = Descriptor();
        var exception = Should.Throw<ArgumentException>(() => new ToolInvocationContext(
            TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, tool, new ToolVersion("9.9"),
            default, Grant(), 1, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddMinutes(1), Progress()));
        exception.ParamName.ShouldBe("toolVersion");
    }

    [Fact]
    public void Constructor_WhenInvocationGrantIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolInvocationContext(
            TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, Descriptor(), ToolVersion(),
            default, null!, 1, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddMinutes(1), Progress())).ParamName.ShouldBe("invocationGrant");

    [Fact]
    public void Constructor_WhenGrantScopeDoesNotMatchIdentity_ThrowsExactParameter()
    {
        var wrongAgentId = new AgentId(Guid.NewGuid());
        var exception = Should.Throw<ArgumentException>(() => new ToolInvocationContext(
            wrongAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, Descriptor(), ToolVersion(),
            default, Grant(), 1, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddMinutes(1), Progress()));
        exception.ParamName.ShouldBe("invocationGrant");
    }

    [Fact]
    public void Constructor_WhenAttemptIsNotPositive_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolInvocationContext(
            TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, Descriptor(), ToolVersion(),
            default, Grant(), 0, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddMinutes(1), Progress())).ParamName.ShouldBe("attempt");

    [Fact]
    public void Constructor_WhenProgressIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolInvocationContext(
            TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, Descriptor(), ToolVersion(),
            default, Grant(), 1, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddMinutes(1), null!)).ParamName.ShouldBe("progress");

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = InvocationContext();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
