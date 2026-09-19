// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies ApprovalAuthenticationEvidence behavior and contracts.</summary>
public sealed class ApprovalAuthenticationEvidenceTests
{
    private static ApprovalAuthenticationEvidence Create(
        ApprovalRequestId requestId,
        OperationCorrelation correlation,
        PrincipalId principalId,
        ApprovalAuthenticationMethod method,
        ContentHash channelBinding) =>
        new(
            new ApprovalAuthenticationEvidenceId("evidence-1"),
            new ApprovalChannelId("cli"),
            requestId,
            correlation,
            principalId,
            method,
            DateTimeOffset.UnixEpoch,
            channelBinding);

    private static ApprovalAuthenticationEvidence CreateValid() => Create(
        new ApprovalRequestId(Guid.NewGuid()),
        SecurityAbstractionsTestData.Correlation(),
        new PrincipalId("approver"),
        ApprovalAuthenticationMethod.OneTimePasscode,
        new ContentHash("sha256:channel"));

    [Fact]
    public void Constructor_WhenRequestIdIsDefault_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Create(default, SecurityAbstractionsTestData.Correlation(), new PrincipalId("approver"), ApprovalAuthenticationMethod.Password, new ContentHash("sha256:channel")))
            .ParamName.ShouldBe("requestId");

    [Fact]
    public void Constructor_WhenCorrelationIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => Create(new ApprovalRequestId(Guid.NewGuid()), null!, new PrincipalId("approver"), ApprovalAuthenticationMethod.Password, new ContentHash("sha256:channel")))
            .ParamName.ShouldBe("correlation");

    [Fact]
    public void Constructor_WhenMethodIsUndefined_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Create(new ApprovalRequestId(Guid.NewGuid()), SecurityAbstractionsTestData.Correlation(), new PrincipalId("approver"), (ApprovalAuthenticationMethod) 99, new ContentHash("sha256:channel")))
            .ParamName.ShouldBe("method");

    [Fact]
    public void Constructor_WhenChannelBindingIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => Create(new ApprovalRequestId(Guid.NewGuid()), SecurityAbstractionsTestData.Correlation(), new PrincipalId("approver"), ApprovalAuthenticationMethod.Password, default))
            .ParamName.ShouldBe("channelBinding");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var requestId = new ApprovalRequestId(Guid.NewGuid());
        var correlation = SecurityAbstractionsTestData.Correlation();
        var principalId = new PrincipalId("approver");
        var evidence = Create(requestId, correlation, principalId, ApprovalAuthenticationMethod.OneTimePasscode, new ContentHash("sha256:channel"));
        evidence.Id.ShouldBe(new ApprovalAuthenticationEvidenceId("evidence-1"));
        evidence.ChannelId.ShouldBe(new ApprovalChannelId("cli"));
        evidence.RequestId.ShouldBe(requestId);
        evidence.Correlation.ShouldBe(correlation);
        evidence.AuthenticatedPrincipalId.ShouldBe(principalId);
        evidence.Method.ShouldBe(ApprovalAuthenticationMethod.OneTimePasscode);
        evidence.AuthenticatedAt.ShouldBe(DateTimeOffset.UnixEpoch);
        evidence.ChannelBinding.ShouldBe(new ContentHash("sha256:channel"));
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = CreateValid();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
