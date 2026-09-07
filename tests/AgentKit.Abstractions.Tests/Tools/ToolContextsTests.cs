// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using System.Text.Json;

using AgentKit;

public sealed class ToolContextsTests
{
    [Fact]
    public void ToolExecutionContext_Constructor_WhenCorrelationNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ToolExecutionContext(AgentId(), SessionId(), ToolCallId(), null!, Identity()));

        exception.ParamName.ShouldBe("correlation");
    }

    [Fact]
    public void ToolExecutionContext_Constructor_WhenIdentityNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ToolExecutionContext(AgentId(), SessionId(), ToolCallId(), Correlation(), null!));

        exception.ParamName.ShouldBe("identity");
    }

    [Fact]
    public void ToolExecutionContext_Constructor_WhenValid_RoundTripsProperties()
    {
        var agentId = AgentId();
        var sessionId = SessionId();
        var callId = ToolCallId();
        var correlation = Correlation();
        var identity = Identity();

        var context = new ToolExecutionContext(agentId, sessionId, callId, correlation, identity);

        context.AgentId.ShouldBe(agentId);
        context.SessionId.ShouldBe(sessionId);
        context.ToolCallId.ShouldBe(callId);
        context.Correlation.ShouldBe(correlation);
        context.Identity.ShouldBe(identity);
    }

    [Fact]
    public void ToolExecutionContext_Equality_WhenSameValues_InstancesAreEqual()
    {
        var agentId = AgentId();
        var sessionId = SessionId();
        var callId = ToolCallId();
        var correlation = Correlation();
        var identity = Identity();

        new ToolExecutionContext(agentId, sessionId, callId, correlation, identity)
            .ShouldBe(new ToolExecutionContext(agentId, sessionId, callId, correlation, identity));
    }

    [Fact]
    public void ToolCallRequest_Constructor_WhenContextNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ToolCallRequest(new ToolId("t"), null!, default, DateTimeOffset.UnixEpoch));

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

    [Fact]
    public void ToolInvocationRequest_Constructor_WhenContextNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ToolInvocationRequest(null!, default, DateTimeOffset.UnixEpoch));

        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void ToolInvocationRequest_Constructor_WhenValid_RoundTripsProperties()
    {
        var context = ExecutionContext();

        var request = new ToolInvocationRequest(context, default, DateTimeOffset.UnixEpoch);

        request.Context.ShouldBe(context);
        request.RequestedAt.ShouldBe(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void ToolAuthorizationRequest_Constructor_WhenContextNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ToolAuthorizationRequest(null!, Descriptor()));

        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void ToolAuthorizationRequest_Constructor_WhenDescriptorNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ToolAuthorizationRequest(ExecutionContext(), null!));

        exception.ParamName.ShouldBe("descriptor");
    }

    [Fact]
    public void ToolAuthorizationRequest_Constructor_WhenValid_RoundTripsProperties()
    {
        var context = ExecutionContext();
        var descriptor = Descriptor();

        var request = new ToolAuthorizationRequest(context, descriptor);

        request.Context.ShouldBe(context);
        request.Descriptor.ShouldBe(descriptor);
    }

    private static AgentId AgentId() => new(Guid.NewGuid());

    private static SessionId SessionId() => new(Guid.NewGuid());

    private static ToolCallId ToolCallId() => new(Guid.NewGuid());

    private static InRunOperationCorrelation Correlation() =>
        new(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null);

    private static ExecutionIdentity Identity() =>
        new(new TenantId("t"), new PrincipalId("p"), ExecutionSubjectKind.Human, ExtensionData.Empty);

    private static ToolExecutionContext ExecutionContext() =>
        new(AgentId(), SessionId(), ToolCallId(), Correlation(), Identity());

    private static ToolDescriptor Descriptor() => new(
        new ToolId("t"), null, "tool", "description", default, ToolEffect.ReadOnly, ExtensionData.Empty);
}
