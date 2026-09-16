// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.AgentLoop;

using AgentKit;
using AgentKit.TestSupport;

/// <summary>
/// Deterministic builders for agent-loop contract values shared across the
/// Loop fixture files.
/// </summary>
internal static class LoopTestData
{
    public static AgentId AgentId { get; } = new(Guid.Parse("b0000000-0000-0000-0000-000000000001"));
    public static SessionId SessionId { get; } = new(Guid.Parse("b0000000-0000-0000-0000-000000000002"));
    public static BranchId BranchId { get; } = new(Guid.Parse("b0000000-0000-0000-0000-000000000003"));
    public static RunId RunId { get; } = new(Guid.Parse("b0000000-0000-0000-0000-000000000004"));
    public static TurnId TurnId { get; } = new(Guid.Parse("b0000000-0000-0000-0000-000000000005"));
    public static OperationId OperationId { get; } = new(Guid.Parse("b0000000-0000-0000-0000-000000000006"));
    public static ModelRequestId ModelRequestId { get; } = new(Guid.Parse("b0000000-0000-0000-0000-000000000007"));

    public static ExecutionIdentity Identity() =>
        TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);

    public static InRunOperationCorrelation InRun() => new(OperationId, RunId, TurnId);

    public static ProviderResponseIdentity ProviderIdentity() =>
        new(new ProviderId("test"), null, new ApiFamilyId("test"), new ModelId("test"), new ModelId("test"), null, null, null);

    public static AssistantResponseMetadata Metadata(NormalizedStopReason stopReason = NormalizedStopReason.Completed) =>
        new(ModelRequestId, ProviderIdentity(), stopReason, null, ModelUsage.NotReported, ExtensionData.Empty);

    public static AssistantMessage AssistantMessage(ImmutableArray<ContentPart>? parts = null) =>
        new(new MessageId(Guid.Parse("b0000000-0000-0000-0000-000000000008")), AgentId, SessionId, null, BranchId, RunId, TurnId,
            DateTimeOffset.UnixEpoch, MessageState.Complete,
            parts ?? [new TextPart("done", TextSemantics.Plain, ExtensionData.Empty)], Metadata(), ExtensionData.Empty);

    public static ProviderFailure Failure(ProviderFailureKind kind = ProviderFailureKind.Unknown, string safeMessage = "failed") =>
        new(kind, new ProviderId("test"), null, null, null, null, safeMessage, null, ExtensionData.Empty);

    public static ContextPreparationFailure PreparationFailure() =>
        new(ContextPreparationFailureKind.Unknown, "unavailable", ExtensionData.Empty);
}
