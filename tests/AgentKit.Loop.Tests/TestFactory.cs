// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

internal static class TestFactory
{
    public static ExecutionIdentity Identity() =>
        TestSupport.TestExecutionIdentity.Create(new TenantId("tenant-1"), new PrincipalId("user-1"), ExecutionSubjectKind.Human);

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

    /// <summary>Builds a policy naming one candidate alias.</summary>
    public static ModelSelectionPolicy Policy(string alias = "chat") =>
        new([new ModelAlias(alias)]);

    /// <summary>Builds a catalog snapshot publishing the supplied descriptors.</summary>
    public static ModelCatalogSnapshot Catalog(params ModelDescriptor[] models) =>
        new(new ModelCatalogVersion(1), [.. models]);

    public static AgentRunRequest RunRequest(
        AgentId agentId,
        SessionId sessionId,
        BranchId branchId,
        RunId? runId = null,
        ModelSelectionPolicy? policy = null,
        ModelRequirements? requirements = null,
        int maxTurns = 8)
    {
        var selectedRunId = runId ?? new RunId(Guid.NewGuid());
        var identity = Identity();
        var correlation = new InRunOperationCorrelation(
            new OperationId(Guid.NewGuid()), selectedRunId, turnId: null);
        return new AgentRunRequest(
            agentId,
            sessionId,
            branchId,
            selectedRunId,
            identity,
            TestSupport.TestSecurityEvidence.Authorization(agentId, sessionId, correlation, identity),
            TestSupport.TestSecurityEvidence.SessionProfile(),
            policy ?? Policy(),
            requirements ?? ModelRequirements.None,
            instructions: [],
            tools: [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            maxTurns,
            TimeSpan.FromMinutes(1),
            ExtensionData.Empty);
    }

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
            ModelUsage.NotReported,
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
            diagnosticCause: null, ExtensionData.Empty);

    public static ProviderFailure Cancellation(string safeMessage = "cancelled") =>
        new(
            ProviderFailureKind.Cancellation,
            new ProviderId("test-provider"),
            requestId: null,
            statusCode: null,
            providerCode: null,
            retryAfter: null,
            safeMessage,
            diagnosticCause: null, ExtensionData.Empty);

    public static ToolInvocationResult SuccessResult(string text = "ok") =>
        new(
            new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty),
            [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)]);
}
