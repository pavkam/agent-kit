// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using System.Text.Json;

using AgentKit;

/// <summary>Verifies ToolCallRequest behavior and contracts.</summary>
public sealed class ToolCallRequestTests
{
    [Fact]
    public void ToolCallRequest_Constructor_WhenContextNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolCallRequest(new ToolId("t"), null!, default, DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void ToolCallRequest_Constructor_WhenValid_RoundTripsProperties()
    {
        var context = ExecutionContext();
        var arguments = JsonDocument.Parse("{}").RootElement;
        var request = new ToolCallRequest(new ToolId("t"), context, arguments, DateTimeOffset.UnixEpoch);
        request.ToolId.ShouldBe(new ToolId("t"));
        request.Context.ShouldBe(context);
        request.RequestedAt.ShouldBe(DateTimeOffset.UnixEpoch);
    }

    private static AgentId AgentId() => new(Guid.NewGuid());
    private static SessionId SessionId() => new(Guid.NewGuid());
    private static ToolCallId ToolCallId() => new(Guid.NewGuid());
    private static InRunOperationCorrelation Correlation() => new(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null);
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("t"), new PrincipalId("p"), ExecutionSubjectKind.Human);
    private static ToolExecutionContext ExecutionContext() => TestSupport.TestSecurityEvidence.ToolContext(AgentId(), SessionId(), ToolCallId(), Correlation(), Identity());
}
