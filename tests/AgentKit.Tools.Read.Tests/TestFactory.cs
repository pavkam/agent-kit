// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Read.Tests;

/// <summary>Provides construction helpers for read-file tool tests.</summary>
internal static class TestFactory
{
    public static ExecutionIdentity Identity() =>
        new(new TenantId("tenant-1"), new PrincipalId("user-1"), ExecutionSubjectKind.Human, ExtensionData.Empty);

    public static OperationCorrelation Correlation() =>
        new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null);

    public static ToolExecutionContext ExecutionContext() => new(
        new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()), new ToolCallId(Guid.NewGuid()), Correlation(), Identity());

    public static ToolInvocationRequest Request(string json) => new(
        ExecutionContext(), JsonDocument.Parse(json).RootElement, DateTimeOffset.UnixEpoch);

    public static string ReadText(ToolInvocationResult result) => ((TextPart) result.Content[0]).Text;
}
