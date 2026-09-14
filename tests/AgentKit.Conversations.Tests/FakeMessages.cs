// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Builds minimal, internally consistent committed messages for <see cref="FakeAgentLoop"/> results.</summary>
internal static class FakeMessages
{
    /// <summary>Builds a complete assistant message carrying a single non-blank text part.</summary>
    public static AssistantMessage Assistant(AgentRunRequest request, string text) =>
        Assistant(request, [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)]);

    /// <summary>Builds a complete assistant message carrying the supplied content parts.</summary>
    public static AssistantMessage Assistant(AgentRunRequest request, ImmutableArray<ContentPart> parts) =>
        Assistant(request, parts, ModelUsage.NotReported);

    /// <summary>Builds a complete assistant message carrying the supplied content parts and reported usage.</summary>
    public static AssistantMessage Assistant(AgentRunRequest request, ImmutableArray<ContentPart> parts, ModelUsage usage) =>
        new(
            new MessageId(Guid.NewGuid()),
            request.AgentId,
            request.SessionId,
            null,
            request.BranchId,
            request.RunId,
            null,
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            parts,
            ResponseMetadata(usage),
            ExtensionData.Empty);

    /// <summary>Builds a complete tool message carrying the supplied content parts.</summary>
    public static ToolMessage Tool(AgentRunRequest request, ImmutableArray<ContentPart> parts) =>
        new(
            new MessageId(Guid.NewGuid()),
            request.AgentId,
            request.SessionId,
            null,
            request.BranchId,
            request.RunId,
            null,
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            parts,
            ExtensionData.Empty);

    /// <summary>Builds a tool-call content part with the given tool name and raw JSON arguments.</summary>
    public static ToolCallPart ToolCall(string toolName, string argumentsJson) =>
        new(
            new ToolCallId(Guid.NewGuid()),
            new ToolReference(new ToolId(toolName), null, toolName),
            JsonDocument.Parse(argumentsJson).RootElement,
            null,
            ExtensionData.Empty);

    /// <summary>Builds a successful tool-result content part answering <paramref name="call"/> with the given text content.</summary>
    public static ToolResultPart ToolSuccess(ToolCallPart call, string resultText) =>
        new(
            call.CallId,
            call.Tool,
            new ToolCallOutcome(
                ToolCallOutcomeKind.Success,
                ToolTerminalStatus.Succeeded,
                SideEffectCertainty.DefinitelyPerformed,
                retryable: false,
                failureReason: null,
                ExtensionData.Empty),
            [new TextPart(resultText, TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);

    /// <summary>Builds a failed tool-result content part answering <paramref name="call"/> with the given safe failure reason.</summary>
    public static ToolResultPart ToolFailure(ToolCallPart call, string failureReason) =>
        new(
            call.CallId,
            call.Tool,
            new ToolCallOutcome(
                ToolCallOutcomeKind.Failed,
                ToolTerminalStatus.InvocationFailed,
                SideEffectCertainty.Unknown,
                retryable: true,
                failureReason: failureReason,
                ExtensionData.Empty),
            [],
            ExtensionData.Empty);

    private static AssistantResponseMetadata ResponseMetadata(ModelUsage usage) =>
        new(
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
            usage,
            ExtensionData.Empty);
}
