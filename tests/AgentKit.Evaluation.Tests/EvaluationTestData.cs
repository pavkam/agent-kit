// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

/// <summary>Builds deterministic, valid evaluation values for tests that exercise one rule at a time.</summary>
internal static class EvaluationTestData
{
    public static EvaluationRunId RunId { get; } = new(Guid.Parse("e1000000-0000-0000-0000-000000000001"));

    public static AgentId Agent { get; } = new(Guid.Parse("a0000000-0000-0000-0000-000000000001"));

    public static SessionProfileKey SessionProfile { get; } = AgentDefinitionFixtures.SessionProfile;

    public static ExecutionIdentity Identity() =>
        TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);

    public static AgentInput Input(string text = "hello", int id = 1) =>
        new(
            new InputId(new Guid(id, 0, 0, [0, 0, 0, 0, 0, 0, 0, 5])),
            InputDelivery.Steer,
            [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);

    public static EvaluationCaseExecution Execution(SessionProfileKey? profile = null) =>
        new(Identity(), profile ?? SessionProfile);

    public static EvaluationCase Case(
        string id = "case-1",
        AgentId? agent = null,
        ImmutableArray<EvaluatorReference>? evaluators = null,
        EvaluationCriteria? criteria = null,
        EvaluationFixtureReference? fixture = null,
        SessionProfileKey? profile = null,
        AgentInput? input = null) =>
        new(
            new EvaluationCaseId(id),
            agent ?? Agent,
            Execution(profile),
            input ?? Input(),
            new AgentRunOptions(),
            evaluators ?? [],
            criteria ?? EvaluationCriteria.Empty,
            fixture);

    public static EvaluationPlan Plan(
        ImmutableArray<EvaluationCase>? cases = null,
        EvaluationExecutionPolicy? execution = null,
        EvaluationRecordingPolicy? recording = null,
        long version = 1) =>
        new(
            new EvaluationPlanId("plan"),
            new EvaluationPlanVersion(version),
            cases ?? [Case()],
            execution ?? EvaluationExecutionPolicy.Default,
            recording ?? EvaluationRecordingPolicy.None);

    public static EvaluationRunManifest Manifest(AgentId? agent = null) =>
        new(
            agent ?? Agent,
            new AgentDefinitionRevision(1),
            new AgentCatalogVersion(1),
            SessionProfile,
            ["chat"],
            [new EvaluationModelUse("provider", "family", "model", null)]);

    public static EvaluationRunRecord Run(int index = 1) =>
        new(
            new RunId(new Guid(index, 0, 0, [0, 0, 0, 0, 0, 0, 0, 3])),
            new SessionId(new Guid(index, 0, 0, [0, 0, 0, 0, 0, 0, 0, 2])),
            "succeeded",
            "completed");

    public static EvaluatorResult Passed(string key = "schema") =>
        new(
            new EvaluatorKey(key),
            new EvaluatorVersion(1),
            new EvaluationPassed(EvaluationScore.Certain(true), "matched", [new EvaluationEvidence("rule", "exact")]),
            TimeSpan.FromMilliseconds(5));

    public static EvaluationCaseResult Result(
        int ordinal = 0,
        int repetition = 1,
        string? caseId = null,
        EvaluationRunId? runId = null,
        long planVersion = 1,
        string plan = "plan",
        EvaluationCaseDisposition disposition = EvaluationCaseDisposition.Evaluated,
        ImmutableArray<EvaluatorResult>? evaluators = null,
        TimeSpan? latency = null) =>
        new(
            runId ?? RunId,
            new EvaluationPlanId(plan),
            new EvaluationPlanVersion(planVersion),
            new EvaluationCaseId(caseId ?? $"case-{ordinal}"),
            ordinal,
            repetition,
            disposition,
            DateTimeOffset.UnixEpoch.AddSeconds((ordinal * 10) + repetition),
            latency ?? TimeSpan.FromMilliseconds(120.5),
            "0af7651916cd43dd8448eb211c80319c",
            disposition == EvaluationCaseDisposition.Evaluated ? Run((ordinal * 10) + repetition) : null,
            Manifest(),
            new EvaluationUsageSummary(1, 10, 12),
            new EvaluationFixtureReference("fixture", "1"),
            evaluators ?? [Passed()],
            [new EvaluationDiagnostic("note", "safe note")]);

    public static AssistantMessage AssistantText(string text, RunId? run = null, IEnumerable<ContentPart>? extraParts = null) =>
        new(
            new MessageId(Guid.NewGuid()),
            RunResultTestData.Agent,
            RunResultTestData.Session,
            null,
            RunResultTestData.Branch,
            run ?? RunResultTestData.Run,
            null,
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty), .. extraParts ?? []],
            new AssistantResponseMetadata(
                new ModelRequestId(Guid.NewGuid()),
                new ProviderResponseIdentity(new ProviderId("test"), null, new ApiFamilyId("test"), new ModelId("m"), new ModelId("m"), null, null, null),
                NormalizedStopReason.Completed,
                null,
                ModelUsage.NotReported,
                ExtensionData.Empty),
            ExtensionData.Empty);

    public static AgentRunFinished<ValidatedOutput> Finished(
        string text = "answer",
        AgentRunOutcome? outcome = null,
        ValidatedOutput? output = null,
        ImmutableArray<AgentMessage>? messages = null) =>
        new(
            RunResultTestData.Agent,
            RunResultTestData.Session,
            null,
            RunResultTestData.Run,
            outcome ?? new RunSucceeded(),
            new RunSettlementCompleted(),
            output ?? new ValidatedOutput(OutputMode.Text, text, null, null),
            RunResultTestData.Cursor,
            messages ?? [AssistantText(text)],
            new RunUsage(RunResultTestData.Run, []),
            [],
            ExtensionData.Empty);

    public static AgentRunRejected<ValidatedOutput> Rejected() =>
        new(RunResultTestData.Agent, RunResultTestData.Session, RunResultTestData.Error(AgentErrorCodes.Cancelled));

    public static EvaluationContext Context(
        AgentRunResult<ValidatedOutput>? result = null,
        EvaluationCase? evaluationCase = null,
        int repetition = 1) =>
        new(
            RunId,
            new EvaluationPlanId("plan"),
            new EvaluationPlanVersion(1),
            evaluationCase ?? Case(),
            repetition,
            result ?? Finished(),
            Manifest(),
            EvaluationUsageSummary.None,
            TimeSpan.FromMilliseconds(10));

    public static ToolCallPart ToolCall(string alias, string argumentsJson = "{}", int id = 1, string? canonical = null)
    {
        using var document = JsonDocument.Parse(argumentsJson);
        return new ToolCallPart(
            new ToolCallId(new Guid(id, 0, 0, [0, 0, 0, 0, 0, 0, 0, 8])),
            new ToolReference(new ToolAlias(alias), canonical is null ? null : new ToolId(canonical), canonical is null ? null : new ToolVersion("1")),
            document.RootElement.Clone(),
            null,
            ExtensionData.Empty);
    }

    public static ToolResultPart ToolResult(ToolCallPart call, bool success = true) =>
        new(
            call.CallId,
            call.Tool,
            new ToolCallOutcome(
                success ? ToolCallOutcomeKind.Success : ToolCallOutcomeKind.Failed,
                success ? ToolTerminalStatus.Succeeded : ToolTerminalStatus.InvocationFailed,
                SideEffectCertainty.DefinitelyPerformed,
                retryable: false,
                success ? null : "failed",
                ExtensionData.Empty),
            [],
            new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0),
            ExtensionData.Empty);

    public static ToolMessage ToolMessage(params ToolResultPart[] results) =>
        new(
            new MessageId(Guid.NewGuid()),
            RunResultTestData.Agent,
            RunResultTestData.Session,
            null,
            RunResultTestData.Branch,
            RunResultTestData.Run,
            null,
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            [.. results],
            ExtensionData.Empty);

    /// <summary>Builds a finished run whose messages hold the given assistant tool calls and their tool results.</summary>
    public static AgentRunFinished<ValidatedOutput> FinishedWithTools(string text, IEnumerable<ToolCallPart> calls, IEnumerable<ToolResultPart> results) =>
        Finished(text, messages: [AssistantText(text, extraParts: calls), ToolMessage([.. results])]);

    public static EvaluationContext ContextFor(AgentRunResult<ValidatedOutput> result, params EvaluationCriterion[] criteria) =>
        Context(result, Case(criteria: new EvaluationCriteria([.. criteria])));
}
