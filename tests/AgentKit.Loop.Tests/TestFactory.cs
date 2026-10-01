// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

internal static class TestFactory
{
    public static ExecutionIdentity Identity() =>
        TestExecutionIdentity.Create(new TenantId("tenant-1"), new PrincipalId("user-1"), ExecutionSubjectKind.Human);

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

    /// <summary>Builds a fully evidenced run request pinned to one exact agent definition.</summary>
    /// <param name="agentId">The agent identity.</param>
    /// <param name="sessionId">The session identity.</param>
    /// <param name="branchId">The branch identity.</param>
    /// <param name="runId">The run identity, or <see langword="null"/> for a fresh one.</param>
    /// <param name="policy">The model policy the definition selects, or <see langword="null"/> for one <c>chat</c> candidate.</param>
    /// <param name="requirements">The model requirements the definition states, or <see langword="null"/> for none.</param>
    /// <param name="maxTurns">The positive turn limit of the request.</param>
    /// <param name="settings">The request settings the definition states, or <see langword="null"/> for the defaults.</param>
    /// <param name="output">The output contract the definition selects, or <see langword="null"/> for free text.</param>
    /// <param name="toolsets">The authored toolsets the definition selects, or <see langword="null"/> for none.</param>
    /// <param name="durabilityProfile">The durability profile the definition selects, or <see langword="null"/> for none.</param>
    /// <returns>A request whose definition, configuration, and authorization agree.</returns>
    public static AgentLoopRunRequest RunRequest(
        AgentId agentId,
        SessionId sessionId,
        BranchId branchId,
        RunId? runId = null,
        ModelSelectionPolicy? policy = null,
        ModelRequirements? requirements = null,
        int maxTurns = 8,
        LlmRequestSettings? settings = null,
        OutputDefinition? output = null,
        ImmutableArray<ToolsetReference>? toolsets = null,
        DurabilityProfileKey? durabilityProfile = null)
    {
        var selectedRunId = runId ?? new RunId(Guid.NewGuid());
        var identity = Identity();
        var correlation = new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), selectedRunId, null);
        var authorization = TestSecurityEvidence.Authorization(agentId, sessionId, correlation, identity);
        var profile = TestSecurityEvidence.SessionProfile();
        var agent = AgentDefinitionFixtures.Create(
            agentId,
            authorization.AgentDefinitionRevision.Value,
            "agent",
            maxTurns,
            models: (policy ?? Policy()) with
            {
                Requirements = requirements ?? ModelRequirements.None,
                RequestSettings = settings ?? LlmRequestSettings.Default,
            },
            output: output,
            toolsets: toolsets,
            securityProfile: new SecurityProfileKey("test-security"),
            sessionProfile: new SessionProfileKey("test-session")) with
        {
            OptionalCapabilities = durabilityProfile is { } selected
                ? new AgentOptionalCapabilitySelection(null, null, selected, null, null, [])
                : AgentOptionalCapabilitySelection.None,
        };
        var configuration = new EffectiveConfigurationSnapshot(
            authorization.ConfigurationVersion,
            profile.ConfigurationFingerprint,
            [],
            []);
        return new AgentLoopRunRequest(
            agent,
            sessionId,
            branchId,
            selectedRunId,
            identity,
            authorization,
            profile,
            configuration,
            maxTurns,
            TimeSpan.FromMinutes(1),
            ExtensionData.Empty);
    }

    /// <summary>Builds the one authored toolset selection whose run-bound capture advertises the fake catalog's tool.</summary>
    /// <returns>A single toolset reference for the loop test catalog.</returns>
    public static ImmutableArray<ToolsetReference> Toolsets() =>
        [new ToolsetReference(new ToolsetKey("test"), new ToolExecutionPolicyKey("test"))];

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

    /// <summary>Builds a complete assistant message entry that requested one tool call, as a crashed run leaves behind.</summary>
    public static MessageSessionEntry SeedAssistantToolCallEntry(
        AgentId agentId, SessionId sessionId, BranchId branchId, long sequence, ToolCallId callId, RunId? runId = null)
    {
        var address = new SessionAddress(agentId, sessionId);
        var priorRunId = runId ?? new RunId(Guid.NewGuid());
        var turnId = new TurnId(Guid.NewGuid());
        return new MessageSessionEntry(
            new SessionEntryId(Guid.NewGuid()),
            address,
            new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), priorRunId, turnId),
            branchId,
            new SessionSequence(sequence),
            null,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            new AssistantMessage(
                new MessageId(Guid.NewGuid()),
                agentId,
                sessionId,
                null,
                branchId,
                priorRunId,
                turnId,
                DateTimeOffset.UnixEpoch,
                MessageState.Complete,
                [new ToolCallPart(callId, new ToolReference(new ToolAlias("search"), null, null), default, null, ExtensionData.Empty)],
                new AssistantResponseMetadata(
                    new ModelRequestId(Guid.NewGuid()),
                    new ProviderResponseIdentity(
                        new ProviderId("test-provider"), null, new ApiFamilyId("test-api"), new ModelId("test-model"),
                        new ModelId("test-model"), null, null, null),
                    NormalizedStopReason.ToolUse,
                    rawStopReason: null,
                    ModelUsage.NotReported,
                    ExtensionData.Empty),
                ExtensionData.Empty));
    }

    /// <summary>Builds a complete tool-result message entry that resolves one prior tool call, as a normal turn commits.</summary>
    public static MessageSessionEntry SeedToolResultEntry(
        AgentId agentId, SessionId sessionId, BranchId branchId, long sequence, ToolCallId callId, RunId runId)
    {
        var address = new SessionAddress(agentId, sessionId);
        return new MessageSessionEntry(
            new SessionEntryId(Guid.NewGuid()),
            address,
            new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), runId, new TurnId(Guid.NewGuid())),
            branchId,
            new SessionSequence(sequence),
            null,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            new ToolMessage(
                new MessageId(Guid.NewGuid()),
                agentId,
                sessionId,
                null,
                branchId,
                runId,
                turnId: null,
                DateTimeOffset.UnixEpoch,
                MessageState.Complete,
                [new ToolResultPart(
                    callId,
                    new ToolReference(new ToolAlias("search"), null, null),
                    new ToolCallOutcome(
                        ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty),
                    [new TextPart("ok", TextSemantics.Plain, ExtensionData.Empty)],
                    new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0),
                    ExtensionData.Empty)],
                ExtensionData.Empty));
    }

    /// <summary>
    /// Builds a compaction entry in the shape the first-party compactor commits: an <see cref="CompactionRecordStatus.Active"/>
    /// record covering <paramref name="coveredStart"/>..<paramref name="coveredEnd"/> whose retained suffix starts at
    /// <paramref name="retainedSuffixStart"/>, or a <see cref="CompactionRecordStatus.Rejected"/> record with the same manifest.
    /// </summary>
    public static CompactionSessionEntry SeedCompactionEntry(
        AgentId agentId,
        SessionId sessionId,
        BranchId branchId,
        long sequence,
        long coveredStart,
        long coveredEnd,
        long retainedSuffixStart,
        string summaryText = "summary of earlier history",
        CompactionRecordStatus status = CompactionRecordStatus.Active,
        CompactionId? compactionId = null,
        RunId? runId = null)
    {
        var address = new SessionAddress(agentId, sessionId);
        var correlation = Correlation(runId);
        var identity = Identity();
        var context = TestSecurityEvidence.CompactionContext(
            compactionId ?? new CompactionId(Guid.NewGuid()), agentId, sessionId, correlation, identity);
        var manifest = new CompactionManifest(
            new CompactionManifestId(Guid.NewGuid()),
            context,
            branchId,
            new SessionVersion(1),
            new CompactionSourceRange(new SessionSequence(coveredStart), new SessionSequence(coveredEnd)),
            new SessionSequence(retainedSuffixStart),
            new CompactionProducer(new CompactionStrategyKey("test"), deterministic: true, ExtensionData.Empty),
            new ContextEpoch(0),
            new CompactionSizeEstimate(10, 100, (int) (coveredEnd - coveredStart + 1)),
            new CompactionSizeEstimate(1, 10, 1),
            DateTimeOffset.UnixEpoch,
            ExtensionData.Empty);
        var active = status == CompactionRecordStatus.Active;
        var record = new CompactionRecord(
            context,
            new SessionVersion(1),
            active ? new SessionVersion(2) : null,
            status,
            manifest,
            active ? new CompactionCheckpoint([new TextPart(summaryText, TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty) : null,
            supersedes: null,
            active ? null : new CompactionRejection(CompactionRejectionKind.NoSafeCut, "rejected in test", ExtensionData.Empty),
            DateTimeOffset.UnixEpoch,
            ExtensionData.Empty);
        return new CompactionSessionEntry(
            new SessionEntryId(Guid.NewGuid()),
            address,
            correlation,
            branchId,
            new SessionSequence(sequence),
            causalParentId: null,
            DateTimeOffset.UnixEpoch.AddMinutes(sequence),
            new SchemaVersion("1"),
            record);
    }

    /// <summary>Builds a non-message fact entry as a tool committing its own session state mid-turn would.</summary>
    public static FakeToolFactSessionEntry SeedToolFactEntry(
        AgentId agentId, SessionId sessionId, BranchId branchId, long sequence) =>
        new(
            new SessionEntryId(Guid.NewGuid()),
            new SessionAddress(agentId, sessionId),
            Correlation(),
            branchId,
            new SessionSequence(sequence));

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
            [new ToolCallPart(callId, new ToolReference(new ToolAlias(toolName), null, null), default, null, ExtensionData.Empty)],
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

    public static ProviderFailure ContextLengthFailure(string safeMessage = "context length exceeded") =>
        new(
            ProviderFailureKind.ContextLengthExceeded,
            new ProviderId("test-provider"),
            requestId: null,
            statusCode: 413,
            providerCode: "context_length_exceeded",
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
            diagnosticCause: null, ExtensionData.Empty);

    public static ToolInvocationResult SuccessResult(string text = "ok") =>
        new(
            new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty),
            [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)]);
}
