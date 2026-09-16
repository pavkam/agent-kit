// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolInvocationRequest behavior and contracts.</summary>
public sealed class ToolInvocationRequestTests
{
    [Fact]
    public void ToolInvocationRequest_Constructor_WhenContextNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolInvocationRequest(null!, default, DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void ToolInvocationRequest_Constructor_WhenValid_RoundTripsProperties()
    {
        var context = ExecutionContext();
        var request = new ToolInvocationRequest(context, default, DateTimeOffset.UnixEpoch);
        request.Context.ShouldBe(context);
        request.Arguments.ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Undefined);
        request.RequestedAt.ShouldBe(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ToolInvocationRequest(ExecutionContext(), default, DateTimeOffset.UnixEpoch);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static AgentId AgentId() => new(Guid.NewGuid());
    private static SessionId SessionId() => new(Guid.NewGuid());
    private static ToolCallId ToolCallId() => new(Guid.NewGuid());
    private static InRunOperationCorrelation Correlation() => new(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null);
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("t"), new PrincipalId("p"), ExecutionSubjectKind.Human);
    private static ToolExecutionContext ExecutionContext() => TestSupport.TestSecurityEvidence.ToolContext(AgentId(), SessionId(), ToolCallId(), Correlation(), Identity());
}
