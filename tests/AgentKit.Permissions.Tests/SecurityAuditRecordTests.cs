// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;
/// <summary>Verifies SecurityAuditRecord behavior and contracts.</summary>
public sealed class SecurityAuditRecordTests
{
    [Fact]
    public void SecurityAuditRecord_WhenRequiredOrPresentOptionalIdentitiesAreDefault_RejectsTheExactParameter()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Record(requestId: new SecurityRequestId())).ParamName.ShouldBe("requestId");
        Should.Throw<ArgumentOutOfRangeException>(() => Record(policyVersion: new SecurityPolicyVersion())).ParamName.ShouldBe("policyVersion");
        Should.Throw<ArgumentOutOfRangeException>(() => Record(grantId: default(GrantId))).ParamName.ShouldBe("grantId");
        Should.Throw<ArgumentOutOfRangeException>(() => Record(approvalRequestId: default(ApprovalRequestId))).ParamName.ShouldBe("approvalRequestId");
    }

    private static SecurityAuditRecord Record(SecurityRequestId? requestId = null, GrantId? grantId = null, ApprovalRequestId? approvalRequestId = null, SecurityPolicyVersion? policyVersion = null, IEnumerable<KeyValuePair<string, RedactedAuditValue>>? fields = null) => new(new SecurityAuditRecordId(Guid.Parse("a1111111-1111-1111-1111-111111111111")), new SecurityAuthorizationScope(new AgentId(Guid.Parse("a2222222-2222-2222-2222-222222222222")), new SessionId(Guid.Parse("a3333333-3333-3333-3333-333333333333")), new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("a4444444-4444-4444-4444-444444444444")), new AdmissionId(Guid.Parse("a5555555-5555-5555-5555-555555555555")))), requestId ?? new SecurityRequestId(Guid.Parse("a6666666-6666-6666-6666-666666666666")), grantId ?? new GrantId(Guid.Parse("a7777777-7777-7777-7777-777777777777")), approvalRequestId, SecurityAuditEventKind.GrantConsumptionIntent, SecurityAuditOutcome.Accepted, policyVersion ?? new SecurityPolicyVersion(1), fields?.ToImmutableDictionary() ?? [], DateTimeOffset.UnixEpoch);
}
