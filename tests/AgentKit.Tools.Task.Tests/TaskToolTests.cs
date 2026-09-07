// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Task.Tests;

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
    [InlineData(/*lang=json,strict*/ "{}")]
    [InlineData(/*lang=json,strict*/ "{\"target_agent_id\":\"bad\",\"objective\":\"x\",\"acceptance_criteria\":[\"y\"],\"allowed_tools\":[]}")]
    [InlineData(/*lang=json,strict*/ "{\"target_agent_id\":\"50000000-0000-0000-0000-000000000005\",\"objective\":\"x\",\"acceptance_criteria\":[],\"allowed_tools\":[]}")]
    [InlineData(/*lang=json,strict*/ "{\"target_agent_id\":\"50000000-0000-0000-0000-000000000005\",\"objective\":\"x\",\"acceptance_criteria\":[\"y\"],\"allowed_tools\":[\"read\",\"read\"]}")]
    [InlineData(/*lang=json,strict*/ "{\"target_agent_id\":\"50000000-0000-0000-0000-000000000005\",\"objective\":\"x\",\"acceptance_criteria\":[\"y\"],\"allowed_tools\":[],\"max_turns\":0}")]
    public async System.Threading.Tasks.Task InvokeAsync_WhenArgumentsInvalid_PerformsNoIdentityAllocationAuthorizationOrDispatch(string json)
    {
        var broker = new RecordingDelegationBroker();
        var authority = new RecordingSecurityAuthority();
        var ids = new FixedDelegationIdGenerator();

        var result = await Tool(broker, authority, ids).InvokeAsync(Request(json), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        ids.Calls.ShouldBe(0);
        authority.Requests.ShouldBeEmpty();
        broker.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async System.Threading.Tasks.Task InvokeAsync_WhenAuthorized_BindsExactScopedChildEvidence()
    {
        var broker = new RecordingDelegationBroker();
        var authority = new RecordingSecurityAuthority();

        var result = await Tool(broker, authority).InvokeAsync(Request(ValidArguments), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        var security = authority.Requests.ShouldHaveSingleItem();
        security.Kind.ShouldBe(SecurityOperationKind.Delegation);
        security.Effect.ShouldBe(SecurityEffect.Create);
        security.Resources.ShouldBe([TaskDelegationSecurityBinding.Resource(TestData.DelegationId)]);
        var prompt = broker.Requests.ShouldHaveSingleItem().Prompt;
        prompt.TargetAgentId.ShouldBe(TestData.TargetAgentId);
        prompt.AllowedTools.ShouldBe([new ToolId("read"), new ToolId("edit")]);
        prompt.Budget.ShouldBe(new TaskDelegationBudget(12, 25));
        prompt.Deadline.ShouldBe(DateTimeOffset.UnixEpoch.AddMinutes(10));
        broker.GrantMatched.ShouldBeTrue();
    }

    [Fact]
    public async System.Threading.Tasks.Task InvokeAsync_WhenAuthorityDenies_PerformsNoDispatch()
    {
        var broker = new RecordingDelegationBroker();

        var result = await Tool(broker, new RecordingSecurityAuthority(false)).InvokeAsync(Request(ValidArguments), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        broker.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async System.Threading.Tasks.Task InvokeAsync_WhenChildSucceeds_ProjectsTypedNonAuthoritativeResult()
    {
        var result = await Tool(new RecordingDelegationBroker(), new RecordingSecurityAuthority()).InvokeAsync(Request(ValidArguments), TestContext.Current.CancellationToken);

        using var json = JsonDocument.Parse(result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
        json.RootElement.GetProperty("child_goal_id").GetString().ShouldBe(TestData.GoalId.ToString());
        json.RootElement.GetProperty("status").GetString().ShouldBe("succeeded");
        json.RootElement.GetProperty("instruction_authority").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async System.Threading.Tasks.Task InvokeAsync_WhenBrokerReturnsDifferentTarget_FailsClosed()
    {
        var broker = new RecordingDelegationBroker
        {
            Result = static request => new TaskDelegationChildResult(request.Prompt.Id, TestData.GoalId, TestData.ParentAgentId, TestData.ChildSessionId, null, null, TaskDelegationStatus.Succeeded, "Injected.", SideEffectCertainty.DefinitelyNotPerformed),
        };

        var result = await Tool(broker, new RecordingSecurityAuthority()).InvokeAsync(Request(ValidArguments), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Content.ShouldBeEmpty();
    }

    [Fact]
    public async System.Threading.Tasks.Task InvokeAsync_WhenChildFails_PreservesTerminalEvidenceWithoutInstructionAuthority()
    {
        var broker = new RecordingDelegationBroker
        {
            Result = static request => new TaskDelegationChildResult(request.Prompt.Id, TestData.GoalId, request.Prompt.TargetAgentId, TestData.ChildSessionId, TestData.AttemptId, TestData.ChildRunId, TaskDelegationStatus.Failed, "Tests failed.", SideEffectCertainty.DefinitelyPerformed),
        };

        var result = await Tool(broker, new RecordingSecurityAuthority()).InvokeAsync(Request(ValidArguments), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.FailureReason.ShouldBe("Tests failed.");
        using var json = JsonDocument.Parse(result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
        json.RootElement.GetProperty("status").GetString().ShouldBe("failed");
        json.RootElement.GetProperty("instruction_authority").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async System.Threading.Tasks.Task InvokeAsync_WhenNoSession_PerformsNoAuthorizationOrDispatch()
    {
        var broker = new RecordingDelegationBroker();
        var authority = new RecordingSecurityAuthority();

        var result = await Tool(broker, authority).InvokeAsync(Request(ValidArguments, withSession: false), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        authority.Requests.ShouldBeEmpty();
        broker.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void AddTaskTool_WhenCalledTwice_RegistersOneToolAndOneIdentitySource()
    {
        var services = new ServiceCollection();

        _ = services.AddTaskTool().AddTaskTool();

        services.Count(descriptor => descriptor.ServiceType == typeof(ITool) && descriptor.ImplementationType == typeof(TaskTool)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IIdentifierGenerator<DelegationId>)).ShouldBe(1);
    }

    private static TaskTool Tool(ITaskDelegationBroker broker, ISecurityAuthority authority, FixedDelegationIdGenerator? ids = null) => new(
        broker, authority, new FixedSecurityRequestIdGenerator(), ids ?? new FixedDelegationIdGenerator(), new FixedTimeProvider(), Options.Create(new TaskToolOptions()));

    private static ToolInvocationRequest Request(string json, bool withSession = true) => new(
        new ToolExecutionContext(
            TestData.ParentAgentId,
            withSession ? TestData.ParentSessionId : null,
            TestData.ToolCallId,
            new InRunOperationCorrelation(TestData.OperationId, TestData.ParentRunId, null),
            TestData.Identity),
        JsonDocument.Parse(json).RootElement,
        DateTimeOffset.UnixEpoch);
}
