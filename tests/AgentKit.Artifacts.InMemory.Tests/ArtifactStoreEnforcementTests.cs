// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory.Tests;

using static ArtifactTestSupport;

/// <summary>Verifies grant consumption refuses every non-exact outcome before state is touched.</summary>
public sealed class ArtifactStoreEnforcementTests
{
    [Fact]
    public void Constructor_WhenADependencyIsInvalid_ThrowsNamingIt()
    {
        var grants = new IntentReceiptGrantStore();
        var ids = new GuidEnforcementIntentIdGenerator();

        Should.Throw<ArgumentNullException>(() => new ArtifactStoreEnforcement(null!, ids, new ComponentId("a"))).ParamName.ShouldBe("grants");
        Should.Throw<ArgumentNullException>(() => new ArtifactStoreEnforcement(grants, null!, new ComponentId("a"))).ParamName.ShouldBe("intentIds");
        Should.Throw<ArgumentException>(() => new ArtifactStoreEnforcement(grants, ids, default)).ParamName.ShouldBe("audience");
    }

    [Fact]
    public async Task ConsumeAsync_WhenTheGrantStoreConsumesFreshly_ReturnsNoDenial()
    {
        var grants = new IntentReceiptGrantStore();
        var enforcement = new ArtifactStoreEnforcement(grants, new GuidEnforcementIntentIdGenerator(), new ComponentId("test.artifacts"));
        var grant = Grant(Identity, SecurityEffect.Observe, ArtifactSecurityBinding.ArtifactResource(Artifact(1)));

        var denial = await enforcement.ConsumeAsync(grant, SecurityEffect.Observe, [ArtifactSecurityBinding.ArtifactResource(Artifact(1))], new InputFingerprint("f"), TestContext.Current.CancellationToken);

        denial.ShouldBeNull();
        grants.ConsumptionCount.ShouldBe(1);
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("unavailable")]
    public async Task ConsumeAsync_WhenNotFreshlyAndExactlyConsumed_ReturnsADeniedFailure(string defect)
    {
        var grants = new IntentReceiptGrantStore
        {
            Status = defect == "revoked" ? GrantConsumptionStatus.Revoked : GrantConsumptionStatus.Consumed,
            Throw = defect == "unavailable",
        };
        var enforcement = new ArtifactStoreEnforcement(grants, new GuidEnforcementIntentIdGenerator(), new ComponentId("test.artifacts"));
        var grant = Grant(Identity, SecurityEffect.Observe, ArtifactSecurityBinding.ArtifactResource(Artifact(1)));

        var denial = await enforcement.ConsumeAsync(grant, SecurityEffect.Observe, [ArtifactSecurityBinding.ArtifactResource(Artifact(1))], new InputFingerprint("f"), TestContext.Current.CancellationToken);

        denial.ShouldNotBeNull().Kind.ShouldBe(ArtifactFailureKind.Denied);
    }

    [Fact]
    public void GuidEnforcementIntentIdGenerator_WhenCalled_CreatesUniqueNonEmptyIdentities()
    {
        var generator = new GuidEnforcementIntentIdGenerator();

        var first = generator.Create();
        var second = generator.Create();

        first.Value.ShouldNotBe(Guid.Empty);
        first.ShouldNotBe(second);
    }
}
