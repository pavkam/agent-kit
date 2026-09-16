// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using System.Text.Json;

using AgentKit;

/// <summary>Verifies ToolAuthorizationRequest behavior and contracts.</summary>
public sealed class ToolAuthorizationRequestTests
{
    [Fact]
    public void ToolAuthorizationRequest_Constructor_WhenContextNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolAuthorizationRequest(null!, Descriptor()));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void ToolAuthorizationRequest_Constructor_WhenDescriptorNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolAuthorizationRequest(ExecutionContext(), null!));
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

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ToolAuthorizationRequest(ExecutionContext(), Descriptor());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static AgentId AgentId() => new(Guid.NewGuid());
    private static SessionId SessionId() => new(Guid.NewGuid());
    private static ToolCallId ToolCallId() => new(Guid.NewGuid());
    private static InRunOperationCorrelation Correlation() => new(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null);
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("t"), new PrincipalId("p"), ExecutionSubjectKind.Human);
    private static ToolExecutionContext ExecutionContext() => TestSupport.TestSecurityEvidence.ToolContext(AgentId(), SessionId(), ToolCallId(), Correlation(), Identity());
    private static ToolDescriptor Descriptor() => new(new ToolId("t"), new ToolVersion("1.0"), "tool", "description", new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), JsonDocument.Parse("{}").RootElement), outputSchema: null, new ToolEffects(ToolEffect.ReadOnly, null, null), new ToolExecutionHints(ToolSchedulingMode.Unspecified, null, null, null), new ToolSourceId("agentkit.tools.tests"), ExtensionData.Empty);
}
