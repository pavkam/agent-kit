// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Tests;

internal static class TestFactory
{
    public static MessageCursor MessageCursor() => new(
        new AgentId(Guid.NewGuid()),
        new SessionId(Guid.NewGuid()),
        conversationId: null,
        new BranchId(Guid.NewGuid()),
        new SessionVersion(1),
        new SessionSequence(0));

    public static ModelDescriptor Model(string alias = "chat")
    {
        var capabilities = new ModelCapabilities(
            supportsSystemInstructions: true,
            supportsStreaming: true,
            supportsToolCalls: true,
            supportsParallelToolCalls: true,
            supportsStructuredOutput: true,
            supportsReasoning: true,
            supportsVisionInput: true,
            ExtensionData.Empty);

        return new ModelDescriptor(
            new ModelAlias(alias),
            new ProviderId("test-provider"),
            new ApiFamilyId("test-api"),
            new ModelId("test-model"),
            deploymentId: null,
            capabilities,
            new ModelLimits(maxContextTokens: 4096, maxOutputTokens: 1024),
            pricing: null,
            ExtensionData.Empty);
    }

    public static UserMessage UserMessage(string text = "hi", MessageState state = MessageState.Complete) => new(
        new MessageId(Guid.NewGuid()),
        new AgentId(Guid.NewGuid()),
        new SessionId(Guid.NewGuid()),
        null,
        new BranchId(Guid.NewGuid()),
        null,
        null,
        DateTimeOffset.UnixEpoch,
        state,
        [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)],
        ExtensionData.Empty);

    public static SystemMessage SystemMessage(string text = "instruction", MessageState state = MessageState.Complete) => new(
        new MessageId(Guid.NewGuid()),
        new AgentId(Guid.NewGuid()),
        new SessionId(Guid.NewGuid()),
        null,
        new BranchId(Guid.NewGuid()),
        null,
        null,
        DateTimeOffset.UnixEpoch,
        state,
        [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)],
        ExtensionData.Empty);

    public static UserMessage UserMessageWithParts(
        ImmutableArray<ContentPart> parts, MessageState state = MessageState.Complete) => new(
        new MessageId(Guid.NewGuid()),
        new AgentId(Guid.NewGuid()),
        new SessionId(Guid.NewGuid()),
        null,
        new BranchId(Guid.NewGuid()),
        null,
        null,
        DateTimeOffset.UnixEpoch,
        state,
        parts,
        ExtensionData.Empty);

    public static SystemMessage SystemMessageWithParts(
        ImmutableArray<ContentPart> parts, MessageState state = MessageState.Complete) => new(
        new MessageId(Guid.NewGuid()),
        new AgentId(Guid.NewGuid()),
        new SessionId(Guid.NewGuid()),
        null,
        new BranchId(Guid.NewGuid()),
        null,
        null,
        DateTimeOffset.UnixEpoch,
        state,
        parts,
        ExtensionData.Empty);

    public static AssistantMessage AssistantMessageWithParts(
        ImmutableArray<ContentPart> parts, MessageState state = MessageState.Complete) => new(
        new MessageId(Guid.NewGuid()),
        new AgentId(Guid.NewGuid()),
        new SessionId(Guid.NewGuid()),
        null,
        new BranchId(Guid.NewGuid()),
        null,
        null,
        DateTimeOffset.UnixEpoch,
        state,
        parts,
        ResponseMetadata(), ExtensionData.Empty);

    public static ToolMessage ToolMessageWithParts(
        ImmutableArray<ContentPart> parts, MessageState state = MessageState.Complete) => new(
        new MessageId(Guid.NewGuid()),
        new AgentId(Guid.NewGuid()),
        new SessionId(Guid.NewGuid()),
        null,
        new BranchId(Guid.NewGuid()),
        null,
        null,
        DateTimeOffset.UnixEpoch,
        state,
        parts, ExtensionData.Empty);

    public static ToolCallPart ToolCall(ToolCallId callId, string toolName = "search") => new(
        callId,
        new ToolReference(new ToolAlias(toolName), null, null),
        default,
        null, ExtensionData.Empty);

    public static ToolResultPart ToolResult(ToolCallId callId, string toolName = "search") => new(
        callId,
        new ToolReference(new ToolAlias(toolName), null, null),
        new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty),
        [new TextPart("ok", TextSemantics.Plain, ExtensionData.Empty)],
        new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0),
        ExtensionData.Empty);

    /// <summary>Builds one evidence-bound assembly request whose history is rebound to the evidence cursor.</summary>
    /// <param name="history">The source history; message coordinates are rebound to the generated cursor.</param>
    /// <param name="instructions">Flat instruction messages published as the agent definition's literal source.</param>
    /// <param name="model">The selected model, or the default test model.</param>
    public static ContextAssemblyRequest AssemblyRequest(
        ImmutableArray<AgentMessage> history,
        ImmutableArray<AgentMessage>? instructions = null,
        ModelDescriptor? model = null)
    {
        var agentId = new AgentId(Guid.NewGuid());
        var sessionId = new SessionId(Guid.NewGuid());
        var branchId = new BranchId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());
        var turnId = new TurnId(Guid.NewGuid());
        var revision = new AgentDefinitionRevision(1);
        var agent = AgentDefinitionFixtures.Create(agentId, revision: revision.Value, displayName: "agent") with
        {
            Instructions = InstructionSourceProjection.FromMessages(instructions ?? [], revision),
        };
        var identity = TestExecutionIdentity.Create(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Human);
        var correlation = new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), runId, turnId);
        var authorization = new SecurityAuthorizationContext(
            new SecurityProfileKey("security"),
            new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(
                new SecurityPolicySnapshotId(Guid.NewGuid()),
                new SecurityPolicyVersion(1),
                new ContentHash("sha256:policy")),
            new ComponentKey<ISecurityAuthority>("authority"),
            revision,
            new ConfigurationVersion(1),
            new SecurityAuthorizationScope(agentId, sessionId, correlation),
            identity);
        var cursor = new MessageCursor(agentId, sessionId, null, branchId, new SessionVersion(1), new SessionSequence(0));
        var historyView = new HistoryView(cursor, HistoryMessageCoordinateAlignment.Align(history, cursor), []);
        var configuration = new EffectiveConfigurationSnapshot(
            new ConfigurationVersion(1),
            new ContentHash("sha256:configuration"),
            [],
            []);
        return new ContextAssemblyRequest(
            agentId,
            sessionId,
            branchId,
            runId,
            turnId,
            new ModelRequestId(Guid.NewGuid()),
            model ?? Model(),
            new ContextAssemblyEvidence(agent, identity, historyView, authorization, configuration),
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
    }

    private static AssistantResponseMetadata ResponseMetadata() => new(
        new ModelRequestId(Guid.NewGuid()),
        new ProviderResponseIdentity(
            new ProviderId("test-provider"),
            null,
            new ApiFamilyId("test-api"),
            new ModelId("test-model"),
            new ModelId("test-model"),
            null,
            null,
            null),
        NormalizedStopReason.Completed,
        null,
        ModelUsage.NotReported,
        ExtensionData.Empty);
}
