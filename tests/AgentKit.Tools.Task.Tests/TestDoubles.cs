// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Task.Tests;

internal sealed class RecordingDelegationCoordinator: IDelegationCoordinator
{
    internal List<DelegationRequest> Requests { get; } = [];
    internal Func<DelegationRequest, DelegationResult> Result { get; set; } = static request => new DelegationChildResult(
        request.Id,
        TestData.GoalId,
        request.TargetAgentId,
        TestData.ChildSessionId,
        TestData.AttemptId,
        TestData.ChildRunId,
        DelegationStatus.Succeeded,
        new StructuredGoalResult("Implemented and verified.", ExtensionData.Empty),
        [],
        GoalBudgetUsage.None,
        SideEffectCertainty.DefinitelyPerformed,
        ExtensionData.Empty);

    public Task<DelegationResult> DelegateAsync(DelegationRequest request, HookDispatchContext? hooks, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return System.Threading.Tasks.Task.FromResult(Result(request));
    }

    public ValueTask<GoalJoinDecision> JoinAsync(GoalJoinRequest request, HookDispatchContext? hooks, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

internal sealed class FixedDelegationIdGenerator: IIdentifierGenerator<DelegationId>
{
    internal int Calls { get; private set; }
    public DelegationId Create()
    {
        Calls++;
        return TestData.DelegationId;
    }
}

internal sealed class FixedTimeProvider: TimeProvider
{
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
}

internal static class TestData
{
    internal static DelegationId DelegationId { get; } = new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    internal static SecurityRequestId SecurityRequestId { get; } = new(Guid.Parse("20000000-0000-0000-0000-000000000002"));
    internal static GrantId GrantId { get; } = new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
    internal static AgentId ParentAgentId { get; } = new(Guid.Parse("40000000-0000-0000-0000-000000000004"));
    internal static AgentId TargetAgentId { get; } = new(Guid.Parse("50000000-0000-0000-0000-000000000005"));
    internal static SessionId ParentSessionId { get; } = new(Guid.Parse("60000000-0000-0000-0000-000000000006"));
    internal static SessionId ChildSessionId { get; } = new(Guid.Parse("70000000-0000-0000-0000-000000000007"));
    internal static RunId ParentRunId { get; } = new(Guid.Parse("80000000-0000-0000-0000-000000000008"));
    internal static RunId ChildRunId { get; } = new(Guid.Parse("90000000-0000-0000-0000-000000000009"));
    internal static GoalId GoalId { get; } = new(Guid.Parse("a0000000-0000-0000-0000-00000000000a"));
    internal static GoalAttemptId AttemptId { get; } = new(Guid.Parse("b0000000-0000-0000-0000-00000000000b"));
    internal static GoalProfileReference Profile { get; } = new(new GoalProfileKey("goals"), new GoalProfileVersion(1));
    internal static ToolCallId ToolCallId { get; } = new(Guid.Parse("c0000000-0000-0000-0000-00000000000c"));
    internal static OperationId OperationId { get; } = new(Guid.Parse("d0000000-0000-0000-0000-00000000000d"));
    internal static ExecutionIdentity Identity { get; } = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
}
