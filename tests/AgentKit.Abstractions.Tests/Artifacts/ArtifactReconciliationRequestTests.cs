// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;

using static AgentKit.Abstractions.Tests.Artifacts.ArtifactContractTestData;

/// <summary>Verifies <see cref="ArtifactReconciliationRequest"/> validation.</summary>
public sealed class ArtifactReconciliationRequestTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var request = Create();
        request.PreparationId.ShouldBe(PreparationId);
        request.AgentId.ShouldBe(AgentId);
        request.SessionId.ShouldBe(SessionId);
        request.Correlation.ShouldBe(Correlation);
        request.Authorization.Identity.ShouldBe(Identity);
        request.IdempotencyKey.ShouldBe(new IdempotencyKey("reconcile"));
    }

    [Fact]
    public void Constructor_WhenPreparationIsEmpty_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactReconciliationRequest(default, AgentId, SessionId, Correlation, Authorization(), new IdempotencyKey("reconcile"))).ParamName.ShouldBe("preparationId");

    [Fact]
    public void Constructor_WhenCorrelationIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ArtifactReconciliationRequest(PreparationId, AgentId, SessionId, null!, Authorization(), new IdempotencyKey("reconcile"))).ParamName.ShouldBe("correlation");

    [Fact]
    public void Constructor_WhenAuthorizationIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ArtifactReconciliationRequest(PreparationId, AgentId, SessionId, Correlation, null!, new IdempotencyKey("reconcile"))).ParamName.ShouldBe("authorization");

    [Fact]
    public void Constructor_WhenAuthorizationScopeDiffers_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ArtifactReconciliationRequest(PreparationId, new AgentId(Guid.Parse("30000000-0000-0000-0000-0000000000ff")), SessionId, Correlation, Authorization(), new IdempotencyKey("reconcile"))).ParamName.ShouldBe("authorization");

    [Fact]
    public void Constructor_WhenIdempotencyKeyIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ArtifactReconciliationRequest(PreparationId, AgentId, SessionId, Correlation, Authorization(), default)).ParamName.ShouldBe("idempotencyKey");

    private static SecurityAuthorizationContext Authorization() => TestSupport.TestSecurityEvidence.Authorization(AgentId, SessionId, Correlation, Identity);

    private static ArtifactReconciliationRequest Create() => new(PreparationId, AgentId, SessionId, Correlation, Authorization(), new IdempotencyKey("reconcile"));
}
