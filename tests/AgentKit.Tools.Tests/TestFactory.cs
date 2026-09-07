// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

/// <summary>Shared construction helpers for tool pipeline tests.</summary>
internal static class TestFactory
{
    public static ExecutionIdentity Identity(string tenant = "tenant-1", string principal = "user-1") =>
        new(new TenantId(tenant), new PrincipalId(principal), ExecutionSubjectKind.Human, ExtensionData.Empty);

    public static OperationCorrelation Correlation() =>
        new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null);

    public static ToolExecutionContext ExecutionContext(ToolCallId? callId = null) => new(
        new AgentId(Guid.NewGuid()),
        new SessionId(Guid.NewGuid()),
        callId ?? new ToolCallId(Guid.NewGuid()),
        Correlation(),
        Identity());

    public static ToolDescriptor Descriptor(string id = "test-tool", ToolEffect effect = ToolEffect.ReadOnly) => new(
        new ToolId(id),
        null,
        id,
        "A test tool.",
        JsonDocument.Parse("{}").RootElement,
        effect,
        ExtensionData.Empty);

    public static ToolCallRequest CallRequest(ToolId toolId, JsonElement? arguments = null) => new(
        toolId,
        ExecutionContext(),
        arguments ?? JsonDocument.Parse("{}").RootElement,
        DateTimeOffset.UnixEpoch);
}
