// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Plan.Tests;

internal sealed class RecordingPlanStateStore: IPlanStateStore
{
    internal List<PlanReadRequest> Reads { get; } = [];
    internal List<PlanReplaceRequest> Replacements { get; } = [];
    internal List<PlanStatusRequest> StatusChanges { get; } = [];
    internal PlanStateResult Result { get; set; } = new PlanStateMissing();

    public ComponentId SecurityAudience { get; } = new("test.plan.store");

    public ValueTask<PlanStateResult> ReadAsync(PlanReadRequest request, CancellationToken cancellationToken = default)
    {
        Reads.Add(request);
        return ValueTask.FromResult(Result);
    }

    public ValueTask<PlanStateResult> ReplaceAsync(PlanReplaceRequest request, CancellationToken cancellationToken = default)
    {
        Replacements.Add(request);
        return ValueTask.FromResult(Result);
    }

    public ValueTask<PlanStateResult> SetStatusAsync(PlanStatusRequest request, CancellationToken cancellationToken = default)
    {
        StatusChanges.Add(request);
        return ValueTask.FromResult(Result);
    }
}

internal sealed class RecordingSecurityAuthority(bool allow = true): ISecurityAuthority
{
    internal List<SecurityRequest> Requests { get; } = [];

    public ValueTask<SecurityDecision> AuthorizeAsync(SecurityRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return !allow
            ? ValueTask.FromResult<SecurityDecision>(new SecurityDenied(
                request.Id,
                new SecurityPolicyVersion(1),
                new SecurityDenial("test.denied", "Denied.")))
            : ValueTask.FromResult<SecurityDecision>(new SecurityAllowed(
                request.Id,
                new SecurityPolicyVersion(1),
                TestData.Grant(request)));
    }
}

internal sealed class RecordingGrantStore: ISecurityGrantStore
{
    internal List<SecurityEnforcementRequest> Enforcements { get; } = [];
    internal GrantConsumptionStatus Status { get; set; } = GrantConsumptionStatus.Consumed;

    public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        CancellationToken cancellationToken = default)
    {
        Enforcements.Add(enforcement);
        var matches = grant.Scope == enforcement.Scope
            && grant.Identity == enforcement.Identity
            && grant.Audience == enforcement.Audience
            && grant.Kind == enforcement.Kind
            && grant.Effect == enforcement.Effect
            && grant.Resources.SequenceEqual(enforcement.Resources)
            && grant.InputFingerprint == enforcement.InputFingerprint;
        var status = matches ? Status : GrantConsumptionStatus.Mismatch;
        return ValueTask.FromResult(new GrantConsumptionResult(status, 0, $"Grant {status}."));
    }

    public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
}

internal sealed class RecordingSessionCoordinator: ISessionCoordinator
{
    internal SessionLoadResult LoadResult { get; set; } = new SessionLoaded(TestData.Descriptor(new SessionVersion(0)));
    internal Queue<SessionPageResult> Pages { get; } = [];
    internal SessionAppendResult? AppendResult { get; set; }
    internal List<SessionAppendRequest> Appends { get; } = [];
    internal int LoadCalls { get; private set; }

    public ValueTask<SessionCreateResult> CreateAsync(SessionCreateRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<SessionLoadResult> LoadAsync(SessionOperationContext context, CancellationToken cancellationToken = default)
    {
        LoadCalls++;
        return ValueTask.FromResult(LoadResult);
    }

    public ValueTask<SessionAppendResult> AppendAsync(SessionAppendRequest request, CancellationToken cancellationToken = default)
    {
        Appends.Add(request);
        return ValueTask.FromResult(AppendResult ?? new SessionAppended(
            new SessionVersion(request.ExpectedVersion.Value + request.Entries.Length),
            request.Entries));
    }

    public ValueTask<SessionPageResult> ReadAsync(SessionReadRequest request, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Pages.Count == 0
            ? new SessionPage([], request.FromSequenceExclusive, false)
            : Pages.Dequeue());

    public ValueTask<SessionBranchResult> BranchAsync(SessionBranchRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<SessionDeleteResult> DeleteAsync(SessionDeleteRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

internal sealed class FixedSecurityRequestIdGenerator: IIdentifierGenerator<SecurityRequestId>
{
    internal int Calls { get; private set; }
    public SecurityRequestId Create()
    {
        Calls++;
        return new SecurityRequestId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    }
}

internal sealed class FixedPlanIdGenerator: IIdentifierGenerator<PlanId>
{
    public PlanId Create() => TestData.PlanId;
}

internal sealed class FixedEntryIdGenerator: IIdentifierGenerator<SessionEntryId>
{
    public SessionEntryId Create() => new(Guid.Parse("20000000-0000-0000-0000-000000000002"));
}

internal sealed class FixedTimeProvider: TimeProvider
{
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
}

internal static class TestData
{
    internal static PlanId PlanId { get; } = new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
    internal static AgentId AgentId { get; } = new(Guid.Parse("40000000-0000-0000-0000-000000000004"));
    internal static SessionId SessionId { get; } = new(Guid.Parse("50000000-0000-0000-0000-000000000005"));
    internal static BranchId BranchId { get; } = new(Guid.Parse("60000000-0000-0000-0000-000000000006"));
    internal static ToolCallId ToolCallId { get; } = new(Guid.Parse("70000000-0000-0000-0000-000000000007"));
    internal static OperationCorrelation Correlation { get; } = new InRunOperationCorrelation(
        new OperationId(Guid.Parse("80000000-0000-0000-0000-000000000008")),
        new RunId(Guid.Parse("90000000-0000-0000-0000-000000000009")),
        null);
    internal static ExecutionIdentity Identity { get; } = AgentKit.TestSupport.TestExecutionIdentity.Create(
        new TenantId("tenant"),
        new PrincipalId("principal"),
        ExecutionSubjectKind.Human);
    internal static SessionOperationContext Context { get; } = new(AgentId, SessionId, Correlation, Identity);

    internal static ImmutableArray<WorkPlanItem> Items(PlanItemStatus first = PlanItemStatus.Pending) =>
    [
        new(new PlanItemId("one"), "First step.", first),
        new(new PlanItemId("two"), "Second step.", PlanItemStatus.Pending),
    ];

    internal static WorkPlan Plan(long revision = 1, PlanItemStatus first = PlanItemStatus.Pending) =>
        new(PlanId, new PlanRevision(revision), "Ship it", Items(first), Identity, DateTimeOffset.UnixEpoch);

    internal static SessionDescriptor Descriptor(SessionVersion version) => new(
        new SessionAddress(AgentId, SessionId),
        null,
        Identity.TenantId,
        Identity.PrincipalId,
        new SessionStoreKey("test"),
        BranchId,
        version,
        SessionLifecycleState.Active,
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch,
        new SchemaVersion("1"),
        ExtensionData.Empty);

    internal static PlanSessionEntry Entry(long sequence = 1, long revision = 1, PlanItemStatus first = PlanItemStatus.Pending) => new(
        new SessionEntryId(Guid.Parse("a0000000-0000-0000-0000-00000000000a")),
        new SessionAddress(AgentId, SessionId),
        Correlation,
        BranchId,
        new SessionSequence(sequence),
        null,
        DateTimeOffset.UnixEpoch,
        new SchemaVersion("1"),
        Plan(revision, first));

    internal static SecurityGrant Grant(SecurityRequest request) => new(
        new GrantId(Guid.Parse("b0000000-0000-0000-0000-00000000000b")),
        request.Id,
        request.Scope,
        request.Identity,
        request.Audience,
        request.Kind,
        request.Effect,
        request.Resources,
        request.InputFingerprint,
        new SecurityPolicyVersion(1),
        new SecurityRevocationVersion(1),
        DateTimeOffset.UnixEpoch,
        request.Deadline,
        1);

    internal static SecurityGrant Grant(
        ComponentId audience,
        SecurityOperationKind kind,
        SecurityEffect effect,
        InputFingerprint fingerprint) => new(
        new GrantId(Guid.Parse("b0000000-0000-0000-0000-00000000000b")),
        new SecurityRequestId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
        new SecurityAuthorizationScope(AgentId, SessionId, Correlation),
        Identity,
        audience,
        kind,
        effect,
        [PlanSecurityBinding.Resource(new SessionAddress(AgentId, SessionId))],
        fingerprint,
        new SecurityPolicyVersion(1),
        new SecurityRevocationVersion(1),
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch.AddMinutes(1),
        1);
}
