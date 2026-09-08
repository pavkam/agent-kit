// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

public sealed class GrantIntentArgumentExceptionExtensionsTests
{
    [Fact]
    public void ThrowIfNotEqual_WhenValuesAreEqualOrNull_DoesNotThrow()
    {
        string? nullValue = null;
        var actual = new ContentHash("sha256:same");
        var reconstructed = new ContentHash("sha256:same");

        Should.NotThrow(() => ArgumentException.ThrowIfNotEqual(nullValue, null));
        Should.NotThrow(() => ArgumentException.ThrowIfNotEqual(actual, reconstructed));
    }

    [Fact]
    public void ThrowIfNotEqual_WhenValuesDiffer_ThrowsExactArgumentExceptionWithInferredParamName()
    {
        var actual = new ContentHash("sha256:actual");

        var exception = Should.Throw<ArgumentException>(() =>
            ArgumentException.ThrowIfNotEqual(actual, new ContentHash("sha256:expected")));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("actual");
    }

    [Fact]
    public void ThrowIfNotEqual_WhenValuesDifferAndParamNameIsExplicit_UsesExplicitParamName()
    {
        var exception = Should.Throw<ArgumentException>(() =>
            ArgumentException.ThrowIfNotEqual(1, 2, "evidence"));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("evidence");
    }

    [Fact]
    public void ThrowIfInvalidIntentReceipt_WhenReceiptIsNull_AcceptsEveryDefinedStatus()
    {
        foreach (var status in Enum.GetValues<GrantConsumptionStatus>())
        {
            Should.NotThrow(() => ArgumentException.ThrowIfInvalidIntentReceipt(status, null));
        }
    }

    [Theory]
    [InlineData(GrantConsumptionStatus.Consumed)]
    [InlineData(GrantConsumptionStatus.Reconciled)]
    public void ThrowIfInvalidIntentReceipt_WhenStatusCarriesAuthorityEvidence_DoesNotThrow(
        GrantConsumptionStatus status)
    {
        var receipt = Receipt();

        Should.NotThrow(() => ArgumentException.ThrowIfInvalidIntentReceipt(status, receipt));
    }

    [Theory]
    [InlineData(GrantConsumptionStatus.Unknown)]
    [InlineData(GrantConsumptionStatus.Tampered)]
    [InlineData(GrantConsumptionStatus.Mismatch)]
    [InlineData(GrantConsumptionStatus.Expired)]
    [InlineData(GrantConsumptionStatus.Revoked)]
    [InlineData(GrantConsumptionStatus.Exhausted)]
    public void ThrowIfInvalidIntentReceipt_WhenUnsuccessfulStatusCarriesReceipt_ThrowsExactArgumentException(
        GrantConsumptionStatus status)
    {
        var receipt = Receipt();

        var exception = Should.Throw<ArgumentException>(() =>
            ArgumentException.ThrowIfInvalidIntentReceipt(status, receipt));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("receipt");
    }

    [Fact]
    public void ThrowIfInvalidIntentReceipt_WhenParamNameIsExplicit_UsesExplicitParamName()
    {
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidIntentReceipt(
            GrantConsumptionStatus.Unknown, Receipt(), "intentReceipt"));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("intentReceipt");
    }

    private static SecurityEnforcementIntentReceipt Receipt()
    {
        var scope = new SecurityAuthorizationScope(
            new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()),
            new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null));
        var identity = TestSupport.TestExecutionIdentity.Create(
            new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var enforcement = new SecurityEnforcementRequest(
            scope, identity, new ComponentId("component"), SecurityOperationKind.StateRead,
            SecurityEffect.Observe, [new ProtectedResource(ProtectedResourceKind.ApplicationState, "resource")],
            new InputFingerprint("sha256:input"), new SecurityRevocationVersion(1));

        return new SecurityEnforcementIntentReceipt(
            new SecurityEnforcementIntentId(Guid.NewGuid()), new GrantId(Guid.NewGuid()),
            new SecurityRequestId(Guid.NewGuid()), enforcement, null, new ContentHash("sha256:effect"),
            DateTimeOffset.UnixEpoch);
    }
}
