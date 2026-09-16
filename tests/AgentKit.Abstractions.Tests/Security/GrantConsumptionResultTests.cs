// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;



/// <summary>Verifies GrantConsumptionResult behavior and contracts.</summary>
public sealed class GrantConsumptionResultTests
{
    [Fact]
    public void GrantConsumptionResult_WhenReceiptRelationIsInvalid_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new GrantConsumptionResult(GrantConsumptionStatus.Expired, 1, "Expired.", Receipt()));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("intentReceipt");
    }

    [Theory]
    [InlineData(GrantConsumptionStatus.Consumed)]
    [InlineData(GrantConsumptionStatus.Reconciled)]
    public void GrantConsumptionResult_WhenReceiptRelationIsValid_RetainsImmutableEvidence(GrantConsumptionStatus status)
    {
        var receipt = Receipt();
        var result = new GrantConsumptionResult(status, 0, "Receipt retained.", receipt);
        result.IntentReceipt.ShouldBeSameAs(receipt);
        typeof(GrantConsumptionResult).GetProperties().ShouldAllBe(static property => property.SetMethod == null);
    }

    [Fact]
    public void GrantConsumptionResult_WhenUsingLegacyConstructor_RetainsBinaryCompatibleShapeWithoutReceipt()
    {
        var result = new GrantConsumptionResult(GrantConsumptionStatus.Consumed, 0, "Consumed.");
        result.Status.ShouldBe(GrantConsumptionStatus.Consumed);
        result.RemainingUses.ShouldBe(0);
        result.SafeMessage.ShouldBe("Consumed.");
        result.IntentReceipt.ShouldBeNull();
    }

    private static SecurityEnforcementIntentReceipt Receipt() => new(IntentId(), GrantId(), RequestId(), Enforcement(), null, new ContentHash("sha256:effect"), DateTimeOffset.UnixEpoch);
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
