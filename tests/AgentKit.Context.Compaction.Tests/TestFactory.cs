// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction.Tests;

/// <summary>Shared construction helpers for compaction tests.</summary>
internal static class TestFactory
{
    public static ExecutionIdentity Identity(string tenant = "tenant-1", string principal = "user-1") =>
        TestSupport.TestExecutionIdentity.Create(new TenantId(tenant), new PrincipalId(principal), ExecutionSubjectKind.Human);

    public static OperationCorrelation Correlation(RunId? runId = null) =>
        new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), runId ?? new RunId(Guid.NewGuid()), null);

    public static CompactionOperationContext CompactionContext(
        AgentId? agentId = null, SessionId? sessionId = null, CompactionId? compactionId = null)
    {
        var selectedAgentId = agentId ?? new AgentId(Guid.NewGuid());
        var selectedSessionId = sessionId ?? new SessionId(Guid.NewGuid());
        var correlation = Correlation();
        var identity = Identity();
        return TestSupport.TestSecurityEvidence.CompactionContext(
            compactionId ?? new CompactionId(Guid.NewGuid()),
            selectedAgentId,
            selectedSessionId,
            correlation,
            identity);
    }

    public static MessageSessionEntry MessageEntry(
        SessionAddress address,
        BranchId branchId,
        long sequence,
        string text = "hello",
        SessionEntryId? causalParentId = null) =>
        new(
            new SessionEntryId(Guid.NewGuid()),
            address,
            Correlation(),
            branchId,
            new SessionSequence(sequence),
            causalParentId,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            new UserMessage(
                new MessageId(Guid.NewGuid()),
                address.AgentId,
                address.SessionId,
                null,
                branchId,
                null,
                null,
                DateTimeOffset.UnixEpoch,
                MessageState.Complete,
                [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)],
                ExtensionData.Empty));

    private static AssistantResponseMetadata ResponseMetadata() => new(
        new ModelRequestId(Guid.NewGuid()),
        new ProviderResponseIdentity(
            new ProviderId("test"),
            null,
            new ApiFamilyId("test"),
            new ModelId("test-model"),
            new ModelId("test-model"),
            null,
            null,
            null),
        NormalizedStopReason.ToolUse,
        null,
        new ModelUsage(null, null, null, null, null, null, ExtensionData.Empty),
        ExtensionData.Empty);

    /// <summary>Builds a causally paired tool-call entry and its terminal tool-result entry.</summary>
    public static (MessageSessionEntry Call, MessageSessionEntry Result) ToolCallPair(
        SessionAddress address, BranchId branchId, long callSequence, long resultSequence)
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var tool = new ToolReference(new ToolId("test-tool"), null, "test-tool");

        var callEntryId = new SessionEntryId(Guid.NewGuid());
        var callEntry = new MessageSessionEntry(
            callEntryId,
            address,
            Correlation(),
            branchId,
            new SessionSequence(callSequence),
            null,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            new AssistantMessage(
                new MessageId(Guid.NewGuid()),
                address.AgentId,
                address.SessionId,
                null,
                branchId,
                null,
                null,
                DateTimeOffset.UnixEpoch,
                MessageState.Complete,
                [new ToolCallPart(callId, tool, default, null, ExtensionData.Empty)],
                ResponseMetadata(),
                ExtensionData.Empty));

        var resultEntry = new MessageSessionEntry(
            new SessionEntryId(Guid.NewGuid()),
            address,
            Correlation(),
            branchId,
            new SessionSequence(resultSequence),
            callEntryId,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            new ToolMessage(
                new MessageId(Guid.NewGuid()),
                address.AgentId,
                address.SessionId,
                null,
                branchId,
                null,
                null,
                DateTimeOffset.UnixEpoch,
                MessageState.Complete,
                [
                    new ToolResultPart(
                        callId,
                        tool,
                        new ToolCallOutcome(ToolCallOutcomeKind.Success, null, ExtensionData.Empty),
                        [new TextPart("tool result", TextSemantics.Plain, ExtensionData.Empty)],
                        ExtensionData.Empty)
                ],
                ExtensionData.Empty));

        return (callEntry, resultEntry);
    }

    public static CompactionRequest Request(
        CompactionOperationContext context,
        BranchId branchId,
        SessionVersion sourceVersion,
        SessionSequence sourceThrough,
        int minimumRetainedEntries = 1,
        double minimumReductionRatio = 0.1,
        int targetInputTokens = 100) => new(
        context,
        branchId,
        sourceVersion,
        sourceThrough,
        new ContextEpoch(0),
        new CompactionTrigger(CompactionTriggerKind.ExplicitMaintenance, "test", null),
        targetInputTokens,
        minimumReductionRatio,
        minimumRetainedEntries,
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch.AddMinutes(5),
        ExtensionData.Empty);

    public static ServiceProvider BuildProvider(Action<CompactionOptions>? configure = null) =>
        new ServiceCollection().AddContextCompaction(configure).BuildServiceProvider();
}
