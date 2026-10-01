// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Task.Tests;

using System.Diagnostics;

using AgentKit.Observability;

using AgentKit.TestSupport;



/// <summary>Verifies TaskTool behavior and contracts.</summary>
public sealed class TaskToolTests
{
    private static string ValidArguments => $$"""
        {
          "target_agent_id": "{{TestData.TargetAgentId}}",
          "objective": "Implement the parser.",
          "acceptance_criteria": ["All focused tests pass."],
          "allowed_tools": ["read", "edit"],
          "max_turns": 12,
          "max_tool_calls": 25,
          "timeout_seconds": 600
        }
        """;

    [Theory]
    [InlineData( /*lang=json,strict*/"{}")]
    [InlineData( /*lang=json,strict*/"{\"target_agent_id\":\"bad\",\"objective\":\"x\",\"acceptance_criteria\":[\"y\"],\"allowed_tools\":[]}")]
    [InlineData( /*lang=json,strict*/"{\"target_agent_id\":\"50000000-0000-0000-0000-000000000005\",\"objective\":\"x\",\"acceptance_criteria\":[],\"allowed_tools\":[]}")]
    [InlineData( /*lang=json,strict*/"{\"target_agent_id\":\"50000000-0000-0000-0000-000000000005\",\"objective\":\"x\",\"acceptance_criteria\":[\"y\"],\"allowed_tools\":[\"read\",\"read\"]}")]
    [InlineData( /*lang=json,strict*/"{\"target_agent_id\":\"50000000-0000-0000-0000-000000000005\",\"objective\":\"x\",\"acceptance_criteria\":[\"y\"],\"allowed_tools\":[],\"max_turns\":0}")]
    [InlineData( /*lang=json,strict*/"{\"target_agent_id\":\"50000000-0000-0000-0000-000000000005\",\"objective\":\"x\",\"acceptance_criteria\":[\"y\"],\"allowed_tools\":[],\"timeout_seconds\":0}")]
    [InlineData( /*lang=json,strict*/"{\"target_agent_id\":\"50000000-0000-0000-0000-000000000005\",\"objective\":\"x\",\"acceptance_criteria\":[\"y\"],\"allowed_tools\":[],\"timeout_seconds\":-1}")]
    [InlineData( /*lang=json,strict*/"{\"target_agent_id\":\"50000000-0000-0000-0000-000000000005\",\"objective\":\"x\",\"acceptance_criteria\":[\"y\"],\"allowed_tools\":[],\"timeout_seconds\":999999999}")]
    [InlineData( /*lang=json,strict*/"{\"target_agent_id\":\"50000000-0000-0000-0000-000000000005\",\"objective\":\"x\",\"acceptance_criteria\":[\"y\"],\"allowed_tools\":[],\"timeout_seconds\":\"soon\"}")]
    public async System.Threading.Tasks.Task InvokeAsync_WhenArgumentsInvalid_PerformsNoIdentityAllocationOrDelegation(string json)
    {
        var coordinator = new RecordingDelegationCoordinator();
        var ids = new FixedDelegationIdGenerator();

        var result = await Tool(coordinator, ids).InvokeAsync(Request(json), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        ids.Calls.ShouldBe(0);
        coordinator.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async System.Threading.Tasks.Task InvokeAsync_WhenValid_BuildsOneCanonicalScopedDelegationRequest()
    {
        var coordinator = new RecordingDelegationCoordinator();

        var result = await Tool(coordinator).InvokeAsync(Request(ValidArguments), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        var request = coordinator.Requests.ShouldHaveSingleItem();
        request.Id.ShouldBe(TestData.DelegationId);
        request.TargetAgentId.ShouldBe(TestData.TargetAgentId);
        request.ParentAgentId.ShouldBe(TestData.ParentAgentId);
        request.ParentSessionId.ShouldBe(TestData.ParentSessionId);
        request.ParentRunId.ShouldBe(TestData.ParentRunId);
        request.ParentGoalId.ShouldBe(RunRootGoal.GoalIdFor(TestData.ParentRunId));
        request.ParentAttemptId.ShouldBe(RunRootGoal.AttemptIdFor(TestData.ParentRunId));
        request.OperationId.ShouldBe(TestData.OperationId);
        request.ProfileKey.ShouldBe(TestData.Profile.Key);
        request.ProfileVersion.ShouldBe(TestData.Profile.Version);
        request.Scope.AllowedTools.ShouldBe([new ToolId("read"), new ToolId("edit")]);
        request.Budget.Budget.ShouldBe(new GoalBudget(12, 25, 0));
        request.Deadline.ShouldBe(DateTimeOffset.UnixEpoch.AddMinutes(10));
        request.AcceptanceCriteria.Criteria.ShouldBe(["All focused tests pass."]);
        request.ChildGoal.Objective.ShouldBe("Implement the parser.");
        request.CancellationMode.ShouldBe(DelegationCancellationMode.CancelWithParent);
        request.JoinStrategyKey.ShouldBe(GoalJoinStrategyKeys.All);
        request.IdempotencyKey.Value.ShouldBe($"task:{TestData.ToolCallId}");
    }

    [Fact]
    public async System.Threading.Tasks.Task InvokeAsync_WhenTheSameToolCallIsRetried_DerivesTheSameIdempotencyKey()
    {
        var coordinator = new RecordingDelegationCoordinator();
        var tool = Tool(coordinator);

        _ = await tool.InvokeAsync(Request(ValidArguments), TestContext.Current.CancellationToken);
        _ = await tool.InvokeAsync(Request(ValidArguments), TestContext.Current.CancellationToken);

        coordinator.Requests.Count.ShouldBe(2);
        coordinator.Requests[0].IdempotencyKey.ShouldBe(coordinator.Requests[1].IdempotencyKey);
    }

    [Fact]
    public async System.Threading.Tasks.Task InvokeAsync_WhenNoGoalProfileIsConfigured_FailsClosedWithoutDelegating()
    {
        var coordinator = new RecordingDelegationCoordinator();
        var ids = new FixedDelegationIdGenerator();

        var result = await ToolWithoutProfile(coordinator, ids).InvokeAsync(Request(ValidArguments), TestContext.Current.CancellationToken);

        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.Unsupported);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        ids.Calls.ShouldBe(0);
        coordinator.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async System.Threading.Tasks.Task InvokeAsync_WhenChildSucceeds_ProjectsTypedNonAuthoritativeResult()
    {
        var result = await Tool(new RecordingDelegationCoordinator()).InvokeAsync(Request(ValidArguments), TestContext.Current.CancellationToken);

        using var json = JsonDocument.Parse(result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
        json.RootElement.GetProperty("child_goal_id").GetString().ShouldBe(TestData.GoalId.ToString());
        json.RootElement.GetProperty("status").GetString().ShouldBe("succeeded");
        json.RootElement.GetProperty("summary").GetString().ShouldBe("Implemented and verified.");
        json.RootElement.GetProperty("instruction_authority").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async System.Threading.Tasks.Task InvokeAsync_WhenCoordinatorReturnsDifferentTarget_FailsClosed()
    {
        var coordinator = new RecordingDelegationCoordinator
        {
            Result = static request => Child(request, DelegationStatus.Succeeded, "Injected.", SideEffectCertainty.DefinitelyNotPerformed, TestData.ParentAgentId),
        };

        var result = await Tool(coordinator).InvokeAsync(Request(ValidArguments), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.ProtocolFailed);
        result.Content.ShouldBeEmpty();
    }

    [Fact]
    public async System.Threading.Tasks.Task InvokeAsync_WhenCoordinatorAnswersADifferentDelegation_FailsClosed()
    {
        var coordinator = new RecordingDelegationCoordinator
        {
            Result = static request => new DelegationRejected(new DelegationId(Guid.NewGuid()), new DelegationRejection(DelegationRejectionKind.PolicyDenied, "Other."), ExtensionData.Empty),
        };

        var result = await Tool(coordinator).InvokeAsync(Request(ValidArguments), TestContext.Current.CancellationToken);

        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.ProtocolFailed);
    }

    [Fact]
    public async System.Threading.Tasks.Task InvokeAsync_WhenSummaryExceedsTheProjectionBound_FailsClosed()
    {
        var coordinator = new RecordingDelegationCoordinator
        {
            Result = static request => Child(request, DelegationStatus.Succeeded, new string('s', 10), SideEffectCertainty.DefinitelyNotPerformed, request.TargetAgentId),
        };
        var tool = new TaskTool(coordinator, new FixedDelegationIdGenerator(), new FixedTimeProvider(), Options.Create(new TaskToolOptions { GoalProfile = TestData.Profile, MaximumSummaryCharacters = 5 }), NullLogger<TaskTool>.Instance);

        var result = await tool.InvokeAsync(Request(ValidArguments), TestContext.Current.CancellationToken);

        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.ProtocolFailed);
    }

    [Theory]
    [InlineData(DelegationRejectionKind.InvalidRequest, ToolTerminalStatus.InvalidArguments)]
    [InlineData(DelegationRejectionKind.UnknownTarget, ToolTerminalStatus.InvalidArguments)]
    [InlineData(DelegationRejectionKind.AmbiguousTarget, ToolTerminalStatus.InvalidArguments)]
    [InlineData(DelegationRejectionKind.Unauthorized, ToolTerminalStatus.Denied)]
    [InlineData(DelegationRejectionKind.PolicyDenied, ToolTerminalStatus.Denied)]
    [InlineData(DelegationRejectionKind.LimitExceeded, ToolTerminalStatus.Denied)]
    [InlineData(DelegationRejectionKind.BudgetUnavailable, ToolTerminalStatus.Denied)]
    [InlineData(DelegationRejectionKind.DeadlineElapsed, ToolTerminalStatus.Denied)]
    [InlineData(DelegationRejectionKind.HandoffUnavailable, ToolTerminalStatus.Unsupported)]
    [InlineData(DelegationRejectionKind.AuditUnavailable, ToolTerminalStatus.Unsupported)]
    public async System.Threading.Tasks.Task InvokeAsync_WhenCoordinatorRejects_PreservesTypedFailureWithoutContent(DelegationRejectionKind kind, ToolTerminalStatus expected)
    {
        var coordinator = new RecordingDelegationCoordinator
        {
            Result = request => new DelegationRejected(request.Id, new DelegationRejection(kind, "Delegation depth exceeded."), ExtensionData.Empty),
        };

        var result = await Tool(coordinator).InvokeAsync(Request(ValidArguments), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(expected.ToOutcomeKind());
        result.Outcome.SourceStatus.ShouldBe(expected);
        result.Outcome.FailureReason.ShouldBe("Delegation depth exceeded.");
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        result.Content.ShouldBeEmpty();
    }

    [Fact]
    public async System.Threading.Tasks.Task InvokeAsync_WhenTurnsToolCallsAndTimeoutOmitted_UsesConfiguredDefaults()
    {
        var coordinator = new RecordingDelegationCoordinator();
        var json = $$"""
            {
              "target_agent_id": "{{TestData.TargetAgentId}}",
              "objective": "Implement the parser.",
              "acceptance_criteria": ["All focused tests pass."],
              "allowed_tools": ["read", "edit"]
            }
            """;

        var result = await Tool(coordinator).InvokeAsync(Request(json), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        var request = coordinator.Requests.ShouldHaveSingleItem();
        request.Budget.Budget.ShouldBe(new GoalBudget(new TaskToolOptions().DefaultMaximumTurns, new TaskToolOptions().DefaultMaximumToolCalls, 0));
        request.Deadline.ShouldBe(DateTimeOffset.UnixEpoch.Add(new TaskToolOptions().DefaultTimeout));
    }

    [Fact]
    public async System.Threading.Tasks.Task InvokeAsync_WhenAcceptanceCriterionExceedsMaximumCharacters_PerformsNoDelegation()
    {
        var coordinator = new RecordingDelegationCoordinator();
        var json = JsonSerializer.Serialize(new
        {
            target_agent_id = TestData.TargetAgentId.Value.ToString(),
            objective = "Implement the parser.",
            acceptance_criteria = new[] { new string('c', 5000) },
            allowed_tools = Array.Empty<string>(),
        });

        var result = await Tool(coordinator).InvokeAsync(Request(json), TestContext.Current.CancellationToken);

        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        coordinator.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async System.Threading.Tasks.Task InvokeAsync_WhenChildIsStillDispatchedAtTheDeadline_ReportsAFailureWithoutClaimingSuccess()
    {
        var coordinator = new RecordingDelegationCoordinator
        {
            Result = static request => Child(request, DelegationStatus.Dispatched, null, SideEffectCertainty.Unknown, request.TargetAgentId),
        };

        var result = await Tool(coordinator).InvokeAsync(Request(ValidArguments), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.FailureReason!.ShouldContain("deadline");
        using var content = JsonDocument.Parse(result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
        content.RootElement.GetProperty("status").GetString().ShouldBe("dispatched");
    }

    [Fact]
    public async System.Threading.Tasks.Task InvokeAsync_WhenCancelled_PropagatesCancellation()
    {
        var coordinator = new CancellingCoordinator();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await new TaskTool(coordinator, new FixedDelegationIdGenerator(), new FixedTimeProvider(), Options.Create(new TaskToolOptions { GoalProfile = TestData.Profile }), NullLogger<TaskTool>.Instance)
                .InvokeAsync(Request(ValidArguments), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(DelegationStatus.Succeeded, SideEffectCertainty.DefinitelyNotPerformed, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyNotPerformed)]
    [InlineData(DelegationStatus.Succeeded, SideEffectCertainty.Unknown, ToolTerminalStatus.Succeeded, SideEffectCertainty.Unknown)]
    [InlineData(DelegationStatus.Failed, SideEffectCertainty.DefinitelyPerformed, ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyPerformed)]
    [InlineData(DelegationStatus.Cancelled, SideEffectCertainty.PartiallyPerformed, ToolTerminalStatus.Cancelled, SideEffectCertainty.PartiallyPerformed)]
    [InlineData(DelegationStatus.Blocked, SideEffectCertainty.Unknown, ToolTerminalStatus.InvocationFailed, SideEffectCertainty.Unknown)]
    [InlineData(DelegationStatus.Succeeded, SideEffectCertainty.NotApplicable, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed)]
    public async System.Threading.Tasks.Task InvokeAsync_WhenChildSettles_PreservesEffectEvidenceAndCancellation(DelegationStatus status, SideEffectCertainty childCertainty, ToolTerminalStatus expectedStatus, SideEffectCertainty expectedCertainty)
    {
        var coordinator = new RecordingDelegationCoordinator
        {
            Result = request => Child(request, status, "Child settled.", childCertainty, request.TargetAgentId),
        };

        var result = await Tool(coordinator).InvokeAsync(Request(ValidArguments), TestContext.Current.CancellationToken);

        result.Outcome.SourceStatus.ShouldBe(expectedStatus);
        result.Outcome.SideEffectCertainty.ShouldBe(expectedCertainty);
        result.Outcome.Retryable.ShouldBeFalse();
        using var content = JsonDocument.Parse(result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
        content.RootElement.GetProperty("side_effect_certainty").GetString().ShouldBe(childCertainty.ToString());
    }

    [Fact]
    public void Constructor_WhenADependencyIsNull_ThrowsArgumentNullException()
    {
        var options = Options.Create(new TaskToolOptions());

        Should.Throw<ArgumentNullException>(() => new TaskTool(null!, new FixedDelegationIdGenerator(), new FixedTimeProvider(), options, NullLogger<TaskTool>.Instance)).ParamName.ShouldBe("coordinator");
        Should.Throw<ArgumentNullException>(() => new TaskTool(new RecordingDelegationCoordinator(), null!, new FixedTimeProvider(), options, NullLogger<TaskTool>.Instance)).ParamName.ShouldBe("delegationIds");
        Should.Throw<ArgumentNullException>(() => new TaskTool(new RecordingDelegationCoordinator(), new FixedDelegationIdGenerator(), null!, options, NullLogger<TaskTool>.Instance)).ParamName.ShouldBe("timeProvider");
        Should.Throw<ArgumentNullException>(() => new TaskTool(new RecordingDelegationCoordinator(), new FixedDelegationIdGenerator(), new FixedTimeProvider(), null!, NullLogger<TaskTool>.Instance)).ParamName.ShouldBe("options");
    }

    private sealed class CancellingCoordinator: IDelegationCoordinator
    {
        public Task<DelegationResult> DelegateAsync(DelegationRequest request, HookDispatchContext? hooks, CancellationToken cancellationToken = default) =>
            throw new OperationCanceledException();

        public ValueTask<GoalJoinDecision> JoinAsync(GoalJoinRequest request, HookDispatchContext? hooks, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private static DelegationChildResult Child(DelegationRequest request, DelegationStatus status, string? summary, SideEffectCertainty certainty, AgentId agent) => new(
        request.Id, TestData.GoalId, agent, TestData.ChildSessionId, TestData.AttemptId, TestData.ChildRunId, status,
        status == DelegationStatus.Succeeded && summary is not null ? new StructuredGoalResult(summary, ExtensionData.Empty) : null,
        [], GoalBudgetUsage.None, certainty, ExtensionData.Empty);

    private static TaskTool Tool(IDelegationCoordinator coordinator, FixedDelegationIdGenerator? ids = null, ILogger<TaskTool>? logger = null) => new(
        coordinator,
        ids ?? new FixedDelegationIdGenerator(),
        new FixedTimeProvider(),
        Options.Create(new TaskToolOptions { GoalProfile = TestData.Profile }),
        logger ?? NullLogger<TaskTool>.Instance);

    private static TaskTool ToolWithoutProfile(IDelegationCoordinator coordinator, FixedDelegationIdGenerator ids) => new(
        coordinator, ids, new FixedTimeProvider(), Options.Create(new TaskToolOptions()), NullLogger<TaskTool>.Instance);

    private static ToolInvocationContext Request(string json, bool withSession = true) => ToolCaptureTestData.FromRequest(new(TestSecurityEvidence.ToolContext(TestData.ParentAgentId, withSession ? TestData.ParentSessionId : null, TestData.ToolCallId, new InRunOperationCorrelation(TestData.OperationId, TestData.ParentRunId, null), TestData.Identity), JsonDocument.Parse(json).RootElement, DateTimeOffset.UnixEpoch), TaskTool.Descriptor);

    [Fact]
    public async System.Threading.Tasks.Task InvokeAsync_WhenObserved_ReportsTheOutcomeWithoutArgumentContent()
    {
        var logger = new RecordingLogger<TaskTool>();
        var tool = Tool(new RecordingDelegationCoordinator(), logger: logger);
        const string json = /*lang=json,strict*/ """{"classified_argument_9137":"classified-argument-9137"}""";
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            static observation => observation.OperationName == AgentKitActivityNames.ExecuteTool
                && Equals(observation.GetTagItem(AgentKitTagNames.ToolId), TaskTool.Id.ToString()));
        using var metrics = new MetricCollector(AgentKitMetricNames.ToolLeafOperationCount);

        var result = await tool.InvokeAsync(Request(json), TestContext.Current.CancellationToken);

        var outcome = result.Outcome.Kind == ToolCallOutcomeKind.Success ? "succeeded" : "rejected";
        activities.Snapshot().ShouldContain(observation =>
            observation.Status == ActivityStatusCode.Ok && Equals(observation.GetTagItem(AgentKitTagNames.Outcome), outcome));
        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(34200);
        entry.Level.ShouldBe(LogLevel.Debug);
        metrics.Snapshot().ShouldContain(measurement => Equals(measurement.Tags[AgentKitTagNames.Outcome], outcome));
        SignalAssertions.ShouldNotContainContent(activities.Snapshot(), logger.Snapshot(), metrics.Snapshot(), "classified-argument-9137");
    }
}
