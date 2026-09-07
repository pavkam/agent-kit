// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Task.Tests;

internal sealed class RecordingDelegationBroker: ITaskDelegationBroker
{
    internal List<TaskDelegationRequest> Requests { get; } = [];
    internal bool GrantMatched { get; private set; }
    internal Func<TaskDelegationRequest, TaskDelegationResult> Result { get; set; } = static request => new TaskDelegationChildResult(
        request.Prompt.Id,
        TestData.GoalId,
        request.Prompt.TargetAgentId,
        TestData.ChildSessionId,
        TestData.AttemptId,
        TestData.ChildRunId,
        TaskDelegationStatus.Succeeded,
        "Implemented and verified.",
        SideEffectCertainty.DefinitelyPerformed);

    public ComponentId SecurityAudience { get; } = new("test.delegation.broker");

    public ValueTask<TaskDelegationResult> DelegateAsync(TaskDelegationRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        GrantMatched = request.Grant.Audience == SecurityAudience
            && request.Grant.Kind == SecurityOperationKind.Delegation
            && request.Grant.Effect == SecurityEffect.Create
            && request.Grant.Resources.SequenceEqual([TaskDelegationSecurityBinding.Resource(request.Prompt.Id)])
            && request.Grant.InputFingerprint == TaskDelegationSecurityBinding.Fingerprint(request.Prompt);
        return ValueTask.FromResult(GrantMatched ? Result(request) : new TaskDelegationRejected(request.Prompt.Id, "Grant mismatch."));
    }
}

internal sealed class RecordingSecurityAuthority(bool allow = true): ISecurityAuthority
{
    internal List<SecurityRequest> Requests { get; } = [];

    public ValueTask<SecurityDecision> AuthorizeAsync(SecurityRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return ValueTask.FromResult<SecurityDecision>(allow
            ? new SecurityAllowed(request.Id, new SecurityPolicyVersion(1), new SecurityGrant(
                TestData.GrantId, request.Id, request.Scope, request.Identity, request.Audience, request.Kind,
                request.Effect, request.Resources, request.InputFingerprint, new SecurityPolicyVersion(1),
                new SecurityRevocationVersion(1), DateTimeOffset.UnixEpoch, request.Deadline, 1))
            : new SecurityDenied(request.Id, new SecurityPolicyVersion(1), new SecurityDenial("test.denied", "Denied.")));
    }
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

internal sealed class FixedSecurityRequestIdGenerator: IIdentifierGenerator<SecurityRequestId>
{
    public SecurityRequestId Create() => TestData.SecurityRequestId;
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
    internal static ToolCallId ToolCallId { get; } = new(Guid.Parse("c0000000-0000-0000-0000-00000000000c"));
    internal static OperationId OperationId { get; } = new(Guid.Parse("d0000000-0000-0000-0000-00000000000d"));
    internal static ExecutionIdentity Identity { get; } = AgentKit.TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
}
