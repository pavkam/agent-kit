// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.WebSearch.Tests;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="NetworkWebSearchEnforcement"/> evidence and receipt validation.</summary>
public sealed class NetworkWebSearchEnforcementTests
{
    private static readonly SecurityEnforcementIntent Intent = new(
        new SecurityEnforcementIntentId(Guid.Parse("41000000-0000-0000-0000-000000000001")),
        null);

    [Fact]
    public async Task Create_WhenGrantIssued_RestatesExactlyWhatTheGrantAuthorizes()
    {
        var grant = await IssueAsync();

        var enforcement = NetworkWebSearchEnforcement.Create(grant);

        enforcement.Scope.ShouldBe(grant.Scope);
        enforcement.Identity.ShouldBe(grant.Identity);
        enforcement.Authorization.ShouldBe(grant.Authorization);
        enforcement.Audience.ShouldBe(grant.Audience);
        enforcement.Kind.ShouldBe(SecurityOperationKind.Network);
        enforcement.Effect.ShouldBe(SecurityEffect.Egress);
        enforcement.Resources.ShouldBe(grant.Resources);
        enforcement.InputFingerprint.ShouldBe(grant.InputFingerprint);
        enforcement.RevocationVersion.ShouldBe(grant.RevocationVersion);
    }

    [Fact]
    public async Task IsFreshExact_WhenStoreConsumedWithMatchingReceipt_ReturnsTrue()
    {
        var (grant, enforcement, consumption) = await ConsumeAsync();

        NetworkWebSearchEnforcement.IsFreshExact(consumption, grant, enforcement, Intent).ShouldBeTrue();
    }

    [Fact]
    public async Task IsFreshExact_WhenGrantWasAlreadyConsumed_ReturnsFalse()
    {
        var (grant, enforcement, _) = await ConsumeAsync();
        var store = new ConsumingGrantStore();
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        _ = await store.ValidateAndConsumeAsync(grant, enforcement, Intent, TestContext.Current.CancellationToken);

        var replay = await store.ValidateAndConsumeAsync(grant, enforcement, Intent, TestContext.Current.CancellationToken);

        replay.Status.ShouldBe(GrantConsumptionStatus.Exhausted);
        NetworkWebSearchEnforcement.IsFreshExact(replay, grant, enforcement, Intent).ShouldBeFalse();
    }

    [Fact]
    public async Task IsFreshExact_WhenReceiptNamesAnotherIntent_ReturnsFalse()
    {
        var (grant, enforcement, consumption) = await ConsumeAsync();
        var other = new SecurityEnforcementIntent(
            new SecurityEnforcementIntentId(Guid.Parse("41000000-0000-0000-0000-000000000002")),
            null);

        NetworkWebSearchEnforcement.IsFreshExact(consumption, grant, enforcement, other).ShouldBeFalse();
    }

    [Fact]
    public async Task IsFreshExact_WhenReceiptEnforcementDiffers_ReturnsFalse()
    {
        var (grant, enforcement, consumption) = await ConsumeAsync();
        var different = enforcement with { Audience = new ComponentId("another.audience") };

        NetworkWebSearchEnforcement.IsFreshExact(consumption, grant, different, Intent).ShouldBeFalse();
    }

    [Fact]
    public async Task DenialMessage_WhenConsumedResultFailedFreshExactValidation_ReturnsFixedMessage()
    {
        var (_, _, consumption) = await ConsumeAsync();

        NetworkWebSearchEnforcement.DenialMessage(consumption)
            .ShouldBe("The grant store did not retain a fresh exact enforcement-intent receipt.");
    }

    [Fact]
    public void DenialMessage_WhenStoreDenied_ReturnsStoreMessage() =>
        NetworkWebSearchEnforcement.DenialMessage(new GrantConsumptionResult(GrantConsumptionStatus.Mismatch, 0, "Mismatch.", null))
            .ShouldBe("Mismatch.");

    private static async Task<SecurityGrant> IssueAsync()
    {
        var store = new ConsumingGrantStore();
        var authority = new GrantingSecurityAuthority(store);
        var context = TestData.Context;
        var decision = await authority.AuthorizeAsync(
            new SecurityRequest(
                new SecurityRequestId(Guid.Parse("21000000-0000-0000-0000-0000000000b1")),
                context.Authorization.Scope,
                context.ToolCallId,
                context.Authorization.Identity,
                context.Authorization,
                new ComponentId("agentkit.tools.websearch.network"),
                SecurityOperationKind.Network,
                SecurityEffect.Egress,
                [new ProtectedResource(ProtectedResourceKind.NetworkEndpoint, "https://search.example.test/query")],
                new InputFingerprint("sha256:abc"),
                DateTimeOffset.UnixEpoch.AddMinutes(1)),
            TestContext.Current.CancellationToken);
        return decision.ShouldBeOfType<SecurityAllowed>().Grant;
    }

    private static async Task<(SecurityGrant Grant, SecurityEnforcementRequest Enforcement, GrantConsumptionResult Consumption)> ConsumeAsync()
    {
        var store = new ConsumingGrantStore();
        var authority = new GrantingSecurityAuthority(store);
        var context = TestData.Context;
        var decision = await authority.AuthorizeAsync(
            new SecurityRequest(
                new SecurityRequestId(Guid.Parse("21000000-0000-0000-0000-0000000000b2")),
                context.Authorization.Scope,
                context.ToolCallId,
                context.Authorization.Identity,
                context.Authorization,
                new ComponentId("agentkit.tools.websearch.network"),
                SecurityOperationKind.Network,
                SecurityEffect.Egress,
                [new ProtectedResource(ProtectedResourceKind.NetworkEndpoint, "https://search.example.test/query")],
                new InputFingerprint("sha256:abc"),
                DateTimeOffset.UnixEpoch.AddMinutes(1)),
            TestContext.Current.CancellationToken);
        var grant = decision.ShouldBeOfType<SecurityAllowed>().Grant;
        var enforcement = NetworkWebSearchEnforcement.Create(grant);
        var consumption = await store.ValidateAndConsumeAsync(grant, enforcement, Intent, TestContext.Current.CancellationToken);
        return (grant, enforcement, consumption);
    }
}
