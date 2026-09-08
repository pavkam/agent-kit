// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

public sealed class GrantIntentContractsTests
{
    [Fact]
    public void SecurityEnforcementIntent_WhenIdentityIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new SecurityEnforcementIntent(default, null));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("id");
    }

    [Fact]
    public void SecurityEnforcementIntent_WhenFenceIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new SecurityEnforcementIntent(IntentId(), default(FencingToken)));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("requiredFence");
    }

    [Fact]
    public void GrantConsumptionResult_WhenReceiptRelationIsInvalid_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new GrantConsumptionResult(
            GrantConsumptionStatus.Expired, 1, "Expired.", Receipt()));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("intentReceipt");
    }

    [Theory]
    [InlineData(GrantConsumptionStatus.Consumed)]
    [InlineData(GrantConsumptionStatus.Reconciled)]
    public void GrantConsumptionResult_WhenReceiptRelationIsValid_RetainsImmutableEvidence(
        GrantConsumptionStatus status)
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
        result.IntentReceipt.ShouldBeNull();
    }

    [Fact]
    public void SecurityEnforcementIntentReceipt_WhenEnforcementIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SecurityEnforcementIntentReceipt(
            IntentId(), GrantId(), RequestId(), null!, null, new ContentHash("sha256:effect"),
            DateTimeOffset.UnixEpoch));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("enforcement");
    }

    [Fact]
    public void SecurityEnforcementIntentReceipt_WhenFenceIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SecurityEnforcementIntentReceipt(
            IntentId(), GrantId(), RequestId(), Enforcement(), default(FencingToken),
            new ContentHash("sha256:effect"), DateTimeOffset.UnixEpoch));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("requiredFence");
    }

    [Fact]
    public void Fingerprint_WhenIntentIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            SecurityEnforcementBinding.Fingerprint(Enforcement(), null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("intent");
    }

    [Fact]
    public void Fingerprint_WhenResourceTextHasDistinctUtf16CodeUnits_ProducesDistinctBindings()
    {
        string[] resourceValues =
        [
            "resource:\ud800",
            "resource:\ud801",
            "resource:\udc00",
            "resource:\udc01",
            "resource:\ufffd",
            "resource:\ud83d\ude00",
        ];

        var fingerprints = resourceValues
            .Select(static value => SecurityEnforcementBinding.Fingerprint(
                Enforcement(value), new SecurityEnforcementIntent(IntentId(), null)))
            .ToArray();

        fingerprints.Distinct().Count().ShouldBe(resourceValues.Length);
    }

    private static SecurityEnforcementIntentReceipt Receipt() => new(
        IntentId(), GrantId(), RequestId(), Enforcement(), null, new ContentHash("sha256:effect"),
        DateTimeOffset.UnixEpoch);

    private static SecurityEnforcementRequest Enforcement(string resourceValue = "session:test")
    {
        var scope = new SecurityAuthorizationScope(
            new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000002")),
            new BeforeRunOperationCorrelation(
                new OperationId(Guid.Parse("30000000-0000-0000-0000-000000000003")), null));
        var identity = TestSupport.TestExecutionIdentity.Create(
            new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        return new SecurityEnforcementRequest(
            scope, identity, new ComponentId("session"), SecurityOperationKind.StateMutation,
            SecurityEffect.Mutate,
            [new ProtectedResource(ProtectedResourceKind.ApplicationState, resourceValue)],
            new InputFingerprint("sha256:input"), new SecurityRevocationVersion(1));
    }

    private static SecurityEnforcementIntentId IntentId() => new(
        Guid.Parse("40000000-0000-0000-0000-000000000004"));

    private static GrantId GrantId() => new(
        Guid.Parse("50000000-0000-0000-0000-000000000005"));

    private static SecurityRequestId RequestId() => new(
        Guid.Parse("60000000-0000-0000-0000-000000000006"));
}
