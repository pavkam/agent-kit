// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

internal static class TestFactory
{
    public static ExecutionIdentity Identity() =>
        new(new TenantId("tenant-1"), new PrincipalId("user-1"), ExecutionSubjectKind.Human, ExtensionData.Empty);

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

    public static AgentRunRequest RunRequest(
        AgentId agentId,
        SessionId sessionId,
        BranchId branchId,
        RunId? runId = null,
        ModelDescriptor? model = null,
        int maxTurns = 8) => new(
        agentId,
        sessionId,
        branchId,
        runId ?? new RunId(Guid.NewGuid()),
        Identity(),
        model ?? Model(),
        instructions: [],
        tools: [],
        ChatToolChoice.Auto,
        ChatRequestSettings.Default,
        maxTurns,
        TimeSpan.FromMinutes(1),
        ExtensionData.Empty);

    public static OperationCorrelation Correlation(RunId? runId = null) =>
        new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), runId ?? new RunId(Guid.NewGuid()), null);

    public static MessageSessionEntry SeedUserMessageEntry(
        AgentId agentId, SessionId sessionId, BranchId branchId, long sequence, string text = "hello")
    {
        var address = new SessionAddress(agentId, sessionId);
        return new MessageSessionEntry(
            new SessionEntryId(Guid.NewGuid()),
            address,
            Correlation(),
            branchId,
            new SessionSequence(sequence),
            null,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1.0"),
            new UserMessage(
                new MessageId(Guid.NewGuid()),
                agentId,
                sessionId,
                null,
                branchId,
                null,
                null,
                DateTimeOffset.UnixEpoch,
                MessageState.Complete,
                [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)],
                ExtensionData.Empty));
    }

    public static MessageSessionEntry SeedIncompleteMessageEntry(
        AgentId agentId, SessionId sessionId, BranchId branchId, long sequence)
    {
        var address = new SessionAddress(agentId, sessionId);
        return new MessageSessionEntry(
            new SessionEntryId(Guid.NewGuid()),
            address,
            Correlation(),
            branchId,
            new SessionSequence(sequence),
            null,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1.0"),
            new UserMessage(
                new MessageId(Guid.NewGuid()),
                agentId,
                sessionId,
                null,
                branchId,
                null,
                null,
                DateTimeOffset.UnixEpoch,
                MessageState.Incomplete,
                [new TextPart("partial", TextSemantics.Plain, ExtensionData.Empty)],
                ExtensionData.Empty));
    }

    public static ModelAttemptCompleted CompletedWithText(ModelRequestId requestId, string text = "answer") =>
        new(Response(requestId, [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)], NormalizedStopReason.Completed));

    public static ModelAttemptCompleted CompletedWithToolCall(ModelRequestId requestId, ToolCallId callId, string toolName = "search") =>
        new(Response(
            requestId,
            [new ToolCallPart(callId, new ToolReference(new ToolId(toolName), null, toolName), default, null, ExtensionData.Empty)],
            NormalizedStopReason.ToolUse));

    public static ModelResponse Response(ModelRequestId requestId, ImmutableArray<ContentPart> parts, NormalizedStopReason stopReason) =>
        new(
            requestId,
            new ProviderResponseIdentity(
                new ProviderId("test-provider"),
                null,
                new ApiFamilyId("test-api"),
                new ModelId("test-model"),
                new ModelId("test-model"),
                null,
                null,
                null),
            parts,
            stopReason,
            ModelUsage.Empty,
            ExtensionData.Empty);

    public static ProviderFailure Failure(string safeMessage = "boom") =>
        new(
            ProviderFailureKind.Unavailable,
            new ProviderId("test-provider"),
            requestId: null,
            statusCode: null,
            providerCode: null,
            retryAfter: null,
            safeMessage,
            diagnosticCause: null,
            ExtensionData.Empty);

    public static ProviderFailure Cancellation(string safeMessage = "cancelled") =>
        new(
            ProviderFailureKind.Cancellation,
            new ProviderId("test-provider"),
            requestId: null,
            statusCode: null,
            providerCode: null,
            retryAfter: null,
            safeMessage,
            diagnosticCause: null,
            ExtensionData.Empty);

    public static ToolInvocationResult SuccessResult(string text = "ok") =>
        new(
            new ToolCallOutcome(ToolCallOutcomeKind.Success, null, ExtensionData.Empty),
            [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)]);
}
