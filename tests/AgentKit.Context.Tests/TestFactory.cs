// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Tests;

internal static class TestFactory
{
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
        new ToolReference(new ToolId(toolName), null, toolName),
        default,
        null, ExtensionData.Empty);

    public static ToolResultPart ToolResult(ToolCallId callId, string toolName = "search") => new(
        callId,
        new ToolReference(new ToolId(toolName), null, toolName),
        new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty),
        [new TextPart("ok", TextSemantics.Plain, ExtensionData.Empty)],
        ExtensionData.Empty);

    public static ContextAssemblyRequest AssemblyRequest(
        ImmutableArray<AgentMessage> history,
        ImmutableArray<AgentMessage>? instructions = null,
        ModelDescriptor? model = null) => new(
        new AgentId(Guid.NewGuid()),
        new SessionId(Guid.NewGuid()),
        new BranchId(Guid.NewGuid()),
        new RunId(Guid.NewGuid()),
        new TurnId(Guid.NewGuid()),
        new ModelRequestId(Guid.NewGuid()),
        model ?? Model(),
        instructions ?? [],
        history,
        [],
        LlmToolChoice.Auto,
        LlmRequestSettings.Default,
        ExtensionData.Empty);

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
