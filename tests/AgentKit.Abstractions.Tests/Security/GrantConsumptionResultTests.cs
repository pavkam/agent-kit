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

    [Theory]
    [InlineData(GrantConsumptionStatus.Consumed)]
    [InlineData(GrantConsumptionStatus.Reconciled)]
    public void GrantConsumptionResult_WhenConsumedOrReconciledHasNoReceipt_ThrowsArgumentException(GrantConsumptionStatus status)
    {
        var exception = Should.Throw<ArgumentException>(() => new GrantConsumptionResult(status, 0, "No receipt.", null));

        exception.ParamName.ShouldBe("intentReceipt");
    }

    [Theory]
    [InlineData(GrantConsumptionStatus.Unknown)]
    [InlineData(GrantConsumptionStatus.Exhausted)]
    [InlineData(GrantConsumptionStatus.Mismatch)]
    public void GrantConsumptionResult_WhenRefusedHasNoReceipt_RetainsItsStatusWithoutEvidence(GrantConsumptionStatus status)
    {
        var result = new GrantConsumptionResult(status, 1, "Refused.", null);

        result.Status.ShouldBe(status);
        result.RemainingUses.ShouldBe(1);
        result.SafeMessage.ShouldBe("Refused.");
        result.IntentReceipt.ShouldBeNull();
    }

    private static SecurityEnforcementIntentReceipt Receipt() => new(IntentId(), GrantId(), RequestId(), Enforcement(), null, new ContentHash("sha256:effect"), DateTimeOffset.UnixEpoch);
    private static SecurityEnforcementRequest Enforcement(string resourceValue = "session:test")
    {
        var scope = new SecurityAuthorizationScope(new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000002")), new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("30000000-0000-0000-0000-000000000003")), null));
        var identity = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        return new SecurityEnforcementRequest(scope, identity, TestSupport.TestSecurityEvidence.Authorization(scope.AgentId, scope.SessionId, scope.Correlation, identity), new ComponentId("session"), SecurityOperationKind.StateMutation, SecurityEffect.Mutate, [new ProtectedResource(ProtectedResourceKind.ApplicationState, resourceValue)], new InputFingerprint("sha256:input"), new SecurityRevocationVersion(1));
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new GrantConsumptionResult(GrantConsumptionStatus.Consumed, 0, "Consumed.", Receipt());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static SecurityEnforcementIntentId IntentId() => new(Guid.Parse("40000000-0000-0000-0000-000000000004"));
    private static GrantId GrantId() => new(Guid.Parse("50000000-0000-0000-0000-000000000005"));
    private static SecurityRequestId RequestId() => new(Guid.Parse("60000000-0000-0000-0000-000000000006"));
}
