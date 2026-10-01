// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

using static ToolRuntimeTestFixture;

/// <summary>Verifies <see cref="ToolExecutionPolicyContext"/> validation.</summary>
public sealed class ToolExecutionPolicyContextTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var context = new ToolExecutionPolicyContext(TestAgentId, TestSessionId, TestRunId, CatalogVersion(), DateTimeOffset.UnixEpoch);

        context.AgentId.ShouldBe(TestAgentId);
        context.SessionId.ShouldBe(TestSessionId);
        context.RunId.ShouldBe(TestRunId);
        context.CatalogVersion.ShouldBe(CatalogVersion());
        context.PlannedAt.ShouldBe(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void Constructor_WhenAgentIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolExecutionPolicyContext(default, TestSessionId, TestRunId, CatalogVersion(), DateTimeOffset.UnixEpoch))
            .ParamName.ShouldBe("agentId");

    [Fact]
    public void Constructor_WhenSessionIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolExecutionPolicyContext(TestAgentId, default, TestRunId, CatalogVersion(), DateTimeOffset.UnixEpoch))
            .ParamName.ShouldBe("sessionId");

    [Fact]
    public void Constructor_WhenRunIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolExecutionPolicyContext(TestAgentId, TestSessionId, default, CatalogVersion(), DateTimeOffset.UnixEpoch))
            .ParamName.ShouldBe("runId");

    [Fact]
    public void Constructor_WhenCatalogVersionIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolExecutionPolicyContext(TestAgentId, TestSessionId, TestRunId, default, DateTimeOffset.UnixEpoch))
            .ParamName.ShouldBe("catalogVersion");
}
