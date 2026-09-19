// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

/// <summary>Shared construction helpers for tool pipeline tests.</summary>
internal static class TestFactory
{
    public static ExecutionIdentity Identity(string tenant = "tenant-1", string principal = "user-1") =>
        TestSupport.TestExecutionIdentity.Create(new TenantId(tenant), new PrincipalId(principal), ExecutionSubjectKind.Human);

    public static OperationCorrelation Correlation() =>
        new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null);

    public static ToolExecutionContext ExecutionContext(ToolCallId? callId = null)
    {
        var agentId = new AgentId(Guid.NewGuid());
        var sessionId = new SessionId(Guid.NewGuid());
        var correlation = Correlation();
        var identity = Identity();
        return TestSupport.TestSecurityEvidence.ToolContext(
            agentId,
            sessionId,
            callId ?? new ToolCallId(Guid.NewGuid()),
            correlation,
            identity);
    }

    public static ToolDescriptor Descriptor(string id = "test-tool", ToolEffect effect = ToolEffect.ReadOnly) =>
        Descriptor(id, effect, JsonDocument.Parse("{}").RootElement);

    public static ToolDescriptor Descriptor(string id, ToolEffect effect, JsonElement inputSchema) => new(
        new ToolId(id),
        new ToolVersion("1.0"),
        id,
        "A test tool.",
        new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), inputSchema),
        outputSchema: null,
        new ToolEffects(effect, null, null),
        new ToolExecutionHints(ToolSchedulingMode.Unspecified, null, null, null),
        new ToolSourceId("agentkit.tools.tests"),
        ExtensionData.Empty);

    public static LegacyToolCallRequest CallRequest(ToolId toolId, JsonElement? arguments = null) => new(
        new ToolReference(new ToolAlias(toolId.Value), null, null),
        ExecutionContext(),
        arguments ?? JsonDocument.Parse("{}").RootElement,
        DateTimeOffset.UnixEpoch);
}
