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

    private static SecurityAuditRecord Record(SecurityAuditRecordId? id = null, SecurityAuditEventKind? eventKind = null, SecurityAuditOutcome? outcome = null) => new(id ?? new SecurityAuditRecordId(Guid.Parse("a1111111-1111-1111-1111-111111111111")), Scope(), new SecurityRequestId(Guid.Parse("a4444444-4444-4444-4444-444444444444")), null, null, eventKind ?? SecurityAuditEventKind.Decision, outcome ?? SecurityAuditOutcome.Accepted, new SecurityPolicyVersion(1), [], DateTimeOffset.UnixEpoch);
    private static SecurityAuthorizationScope Scope() => new(new AgentId(Guid.Parse("a2222222-2222-2222-2222-222222222222")), null, new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("a3333333-3333-3333-3333-333333333333")), null));
}
