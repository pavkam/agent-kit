// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies typed grant-store revocation outcomes for minimal store implementations.</summary>
public sealed class ISecurityRevocationGenerationTests
{
    [Fact]
    public async Task RevokeAsync_WhenStoreReportsMissing_ReturnsNotFound()
    {
        ISecurityGrantStore store = new MinimalRevokeGrantStore(found: false);
        var grantId = new GrantId(Guid.Parse("50000000-0000-0000-0000-000000000005"));
        var reason = new RevocationReason(SecurityRevocationTrigger.Explicit, "Revoked.");

        _ = (await store.RevokeAsync(grantId, reason, TestContext.Current.CancellationToken))
            .ShouldBeOfType<GrantRevocationNotFound>();
    }

    [Fact]
    public async Task RevokeAsync_WhenStoreReportsRevoked_ReturnsRevoked()
    {
        ISecurityGrantStore store = new MinimalRevokeGrantStore(found: true);
        var grantId = new GrantId(Guid.Parse("50000000-0000-0000-0000-000000000005"));
        var reason = new RevocationReason(SecurityRevocationTrigger.Explicit, "Revoked.");

        _ = (await store.RevokeAsync(grantId, reason, TestContext.Current.CancellationToken))
            .ShouldBeOfType<GrantRevoked>();
    }

    private sealed class MinimalRevokeGrantStore(bool found): ISecurityGrantStore
    {
        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
            SecurityGrant grant,
            SecurityEnforcementRequest enforcement,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new GrantConsumptionResult(GrantConsumptionStatus.Unknown, 0, "unsupported"));

        public ValueTask<GrantRevocationResult> RevokeAsync(
            GrantId grantId,
            RevocationReason reason,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<GrantRevocationResult>(
                found
                    ? new GrantRevoked(grantId, reason)
                    : new GrantRevocationNotFound(grantId));
    }
}
