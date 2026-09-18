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
    public static ExecutionLaneId ExecutionLaneId { get; } = new(Guid.Parse("b0000000-0000-0000-0000-00000000000c"));

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

    public static ToolCallPart ToolCall() =>
        new(new ToolCallId(Guid.Parse("b0000000-0000-0000-0000-000000000009")), new ToolReference(new ToolAlias("t"), new ToolId("t"), new ToolVersion("1")), default, null, ExtensionData.Empty);

    public static OutputRejected OutputRejected() =>
        new(new OutputValidationFailure(OutputValidationFailureKind.SchemaValidationFailed, "invalid", []));

    public static OutputConfigurationRejected OutputConfigurationRejected() =>
        new(new OutputSchemaConfigurationFailure(OutputSchemaConfigurationFailureKind.MalformedSchema, "invalid", []));

    public static SecurityAuthorizationContext RunAuthorization(ConfigurationVersion? configurationVersion = null) =>
        new(
            new SecurityProfileKey("security"),
            new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(
                new SecurityPolicySnapshotId(Guid.Parse("b0000000-0000-0000-0000-00000000000a")),
                new SecurityPolicyVersion(1),
                new ContentHash("sha256:policy")),
            new ComponentKey<ISecurityAuthority>("authority"),
            new AgentDefinitionRevision(1),
            configurationVersion ?? new ConfigurationVersion(1),
            new SecurityAuthorizationScope(AgentId, SessionId, InRun()),
            Identity());

    public static SessionProfileSnapshot SessionProfile() => TestSecurityEvidence.SessionProfile();

    public static AgentRunRequest RunRequest() =>
        new(
            AgentId,
            SessionId,
            BranchId,
            RunId,
            Identity(),
            RunAuthorization(),
            SessionProfile(),
            new ModelSelectionPolicy([new ModelAlias("chat")]),
            ModelRequirements.None,
            [],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            8,
            TimeSpan.FromMinutes(1),
            ExtensionData.Empty);

    public static InputPromotionSnapshot PromotionSnapshot() =>
        new(AgentId, SessionId, new ExecutionLaneId(Guid.Parse("b0000000-0000-0000-0000-00000000000b")), InRun(),
            new OperationStateRevision(1), new SessionBranchCursor(BranchId, null), new SessionSequence(1), null, null,
            PromotionBoundary.BeforeFirstModelRequest, null, TurnId, [new AdmissionId(Guid.Parse("b0000000-0000-0000-0000-00000000000c"))]);

    public static OutputRetryRequired RetryRequired() =>
        new(new OutputRepairInstruction("fix the output"), new OutputValidationFailure(OutputValidationFailureKind.SchemaValidationFailed, "invalid", []));

    public static CommittedToolResultReference ToolResultReference(int value = 1) =>
        new(new SessionEntryId(Guid.Parse($"b0000000-0000-0000-0000-0000000000{value:D2}")), new ToolCallId(Guid.Parse($"b1000000-0000-0000-0000-0000000000{value:D2}")), TurnId);

    public static AgentDefinition Definition() =>
        new(AgentId, new AgentDefinitionRevision(1), "agent", new ModelSelectionPolicy([new ModelAlias("chat")]),
            ModelRequirements.None, [], [], LlmToolChoice.Auto, LlmRequestSettings.Default,
            new RunPolicyDefaults(8, TimeSpan.FromMinutes(1)), ExtensionData.Empty,
            new SecurityProfileKey("security"), new SessionProfileKey("session"));

    public static EffectiveConfigurationSnapshot Configuration() =>
        new(new ConfigurationVersion(1), new ContentHash("sha256:test-session-profile"), [], []);

    public static AgentRunRequest RunRequestFromDefinition() =>
        new(Definition(), SessionId, BranchId, RunId, Identity(), RunAuthorization(), SessionProfile(), Configuration(),
            8, TimeSpan.FromMinutes(1), ExtensionData.Empty);

    public static ToolResultPart ToolResult() =>
        new(new ToolCallId(Guid.Parse("b0000000-0000-0000-0000-000000000009")), new ToolReference(new ToolAlias("t"), new ToolId("t"), new ToolVersion("1")),
            new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty),
            [new TextPart("ok", TextSemantics.Plain, ExtensionData.Empty)],
            new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0),
            ExtensionData.Empty);
}
