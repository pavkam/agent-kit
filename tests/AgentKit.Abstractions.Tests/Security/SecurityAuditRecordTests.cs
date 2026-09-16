// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;



/// <summary>Verifies SecurityAuditRecord behavior and contracts.</summary>
public sealed class SecurityAuditRecordTests
{
    [Fact]
    public void Constructor_WhenAuditRecordHasDefaultIdentityOrUndefinedClassification_ThrowsForTheOwningParameter()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Record(id: new SecurityAuditRecordId())).ParamName.ShouldBe("id");
        Should.Throw<ArgumentOutOfRangeException>(() => Record(eventKind: (SecurityAuditEventKind) 99)).ParamName.ShouldBe("eventKind");
        Should.Throw<ArgumentOutOfRangeException>(() => Record(outcome: (SecurityAuditOutcome) 99)).ParamName.ShouldBe("outcome");
    }

    [Fact]
    public void Constructor_WhenAuditRecordHasNullScopeOrFields_ThrowsWithExactParameterNames()
    {
        var id = new SecurityAuditRecordId(Guid.Parse("a1111111-1111-1111-1111-111111111111"));
        var requestId = new SecurityRequestId(Guid.Parse("a4444444-4444-4444-4444-444444444444"));
        var policyVersion = new SecurityPolicyVersion(1);
        Should.Throw<ArgumentNullException>(() => new SecurityAuditRecord(id, null!, requestId, null, null, SecurityAuditEventKind.Decision, SecurityAuditOutcome.Accepted, policyVersion, [], DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("scope");
        Should.Throw<ArgumentNullException>(() => new SecurityAuditRecord(id, Scope(), requestId, null, null, SecurityAuditEventKind.Decision, SecurityAuditOutcome.Accepted, policyVersion, null!, DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("fields");
    }

    [Fact]
    public void Constructor_WhenRequestIdIsDefault_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SecurityAuditRecord(new SecurityAuditRecordId(Guid.Parse("a1111111-1111-1111-1111-111111111111")), Scope(), default, null, null, SecurityAuditEventKind.Decision, SecurityAuditOutcome.Accepted, new SecurityPolicyVersion(1), [], DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("requestId");

    [Fact]
    public void Constructor_WhenPolicyVersionIsNotPositive_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Record(policyVersion: default(SecurityPolicyVersion))).ParamName.ShouldBe("policyVersion");

    [Fact]
    public void Constructor_WhenGrantIdIsDefault_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Record(grantId: default(GrantId))).ParamName.ShouldBe("grantId");

    [Fact]
    public void Constructor_WhenApprovalRequestIdIsDefault_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Record(approvalRequestId: default(ApprovalRequestId))).ParamName.ShouldBe("approvalRequestId");

    [Fact]
    public void Constructor_WhenFieldKeyIsBlank_ThrowsExactArgumentException()
    {
        var fields = ImmutableDictionary<string, RedactedAuditValue>.Empty.Add(" ", RedactedAuditValue.FromComponentId(new ComponentId("component")));
        Should.Throw<ArgumentException>(() => Record(fields: fields)).ParamName.ShouldBe("fields");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var grantId = new GrantId(Guid.Parse("a5555555-5555-5555-5555-555555555555"));
        var approvalRequestId = new ApprovalRequestId(Guid.Parse("a6666666-6666-6666-6666-666666666666"));
        var fields = ImmutableDictionary<string, RedactedAuditValue>.Empty.Add("key", RedactedAuditValue.FromComponentId(new ComponentId("component")));
        var record = Record(grantId: grantId, approvalRequestId: approvalRequestId, fields: fields);
        record.Id.ShouldBe(new SecurityAuditRecordId(Guid.Parse("a1111111-1111-1111-1111-111111111111")));
        record.Scope.ShouldBe(Scope());
        record.RequestId.ShouldBe(new SecurityRequestId(Guid.Parse("a4444444-4444-4444-4444-444444444444")));
        record.GrantId.ShouldBe(grantId);
        record.ApprovalRequestId.ShouldBe(approvalRequestId);
        record.EventKind.ShouldBe(SecurityAuditEventKind.Decision);
        record.Outcome.ShouldBe(SecurityAuditOutcome.Accepted);
        record.PolicyVersion.ShouldBe(new SecurityPolicyVersion(1));
        record.Fields.ShouldBe(fields);
        record.OccurredAt.ShouldBe(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Record();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static SecurityAuditRecord Record(SecurityAuditRecordId? id = null, SecurityAuditEventKind? eventKind = null,
        SecurityAuditOutcome? outcome = null, SecurityPolicyVersion? policyVersion = null, GrantId? grantId = null,
        ApprovalRequestId? approvalRequestId = null, ImmutableDictionary<string, RedactedAuditValue>? fields = null) =>
        new(id ?? new SecurityAuditRecordId(Guid.Parse("a1111111-1111-1111-1111-111111111111")), Scope(),
            new SecurityRequestId(Guid.Parse("a4444444-4444-4444-4444-444444444444")), grantId, approvalRequestId,
            eventKind ?? SecurityAuditEventKind.Decision, outcome ?? SecurityAuditOutcome.Accepted,
            policyVersion ?? new SecurityPolicyVersion(1), fields ?? [], DateTimeOffset.UnixEpoch);
    private static SecurityAuthorizationScope Scope() => new(new AgentId(Guid.Parse("a2222222-2222-2222-2222-222222222222")), null, new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("a3333333-3333-3333-3333-333333333333")), null));
}
