// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies <see cref="ISecurityGrantStore"/> typed revocation default mapping.</summary>
public sealed class ISecurityRevocationGenerationTests
{
    [Fact]
    public async Task RevokeAsync_WhenLegacyStoreReturnsFalse_ReportsNotFound()
    {
        ISecurityGrantStore store = new LegacyRevokeGrantStore(revokeReturns: false);
        var grantId = new GrantId(Guid.Parse("50000000-0000-0000-0000-000000000005"));
        var reason = new RevocationReason(SecurityRevocationTrigger.Explicit, "Revoked.");

        (await store.RevokeAsync(grantId, reason, TestContext.Current.CancellationToken))
            .ShouldBeOfType<GrantRevocationNotFound>();
    }

    [Fact]
    public async Task RevokeAsync_WhenLegacyStoreReturnsTrue_ReportsRevoked()
    {
        ISecurityGrantStore store = new LegacyRevokeGrantStore(revokeReturns: true);
        var grantId = new GrantId(Guid.Parse("50000000-0000-0000-0000-000000000005"));
        var reason = new RevocationReason(SecurityRevocationTrigger.Explicit, "Revoked.");

        (await store.RevokeAsync(grantId, reason, TestContext.Current.CancellationToken))
            .ShouldBeOfType<GrantRevoked>();
    }

    private sealed class LegacyRevokeGrantStore(bool revokeReturns): ISecurityGrantStore
    {
        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
            SecurityGrant grant,
            SecurityEnforcementRequest enforcement,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new GrantConsumptionResult(GrantConsumptionStatus.Unknown, 0, "unsupported"));

        public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(revokeReturns);
    }
}
