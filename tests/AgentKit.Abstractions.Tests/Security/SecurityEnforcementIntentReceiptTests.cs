// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;



/// <summary>Verifies SecurityEnforcementIntentReceipt behavior and contracts.</summary>
public sealed class SecurityEnforcementIntentReceiptTests
{
    [Fact]
    public void SecurityEnforcementIntentReceipt_WhenEnforcementIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SecurityEnforcementIntentReceipt(IntentId(), GrantId(), RequestId(), null!, null, new ContentHash("sha256:effect"), DateTimeOffset.UnixEpoch));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("enforcement");
    }

    [Fact]
    public void SecurityEnforcementIntentReceipt_WhenFenceIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SecurityEnforcementIntentReceipt(IntentId(), GrantId(), RequestId(), Enforcement(), default(FencingToken), new ContentHash("sha256:effect"), DateTimeOffset.UnixEpoch));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("requiredFence");
    }

    private static SecurityEnforcementRequest Enforcement(string resourceValue = "session:test")
    {
        var scope = new SecurityAuthorizationScope(new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000002")), new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("30000000-0000-0000-0000-000000000003")), null));
        var identity = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        return new SecurityEnforcementRequest(scope, identity, new ComponentId("session"), SecurityOperationKind.StateMutation, SecurityEffect.Mutate, [new ProtectedResource(ProtectedResourceKind.ApplicationState, resourceValue)], new InputFingerprint("sha256:input"), new SecurityRevocationVersion(1));
    }

    private static SecurityEnforcementIntentId IntentId() => new(Guid.Parse("40000000-0000-0000-0000-000000000004"));
    private static GrantId GrantId() => new(Guid.Parse("50000000-0000-0000-0000-000000000005"));
    private static SecurityRequestId RequestId() => new(Guid.Parse("60000000-0000-0000-0000-000000000006"));
}
