// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

public sealed class DenyAllSecurityAuthorityTests
{
    [Fact]
    public async Task AuthorizeAsync_WhenRequestIsValid_DeniesWithoutGrant()
    {
        var authority = new DenyAllSecurityAuthority();
        var grant = InMemorySecurityGrantStoreTests.CreateGrantForTests();
        var request = new SecurityRequest(
            grant.RequestId,
            grant.Scope,
            null,
            grant.Identity,
            grant.Audience,
            grant.Kind,
            grant.Effect,
            grant.Resources,
            grant.InputFingerprint,
            grant.ExpiresAt);

        var decision = await authority.AuthorizeAsync(request, TestContext.Current.CancellationToken);

        var denied = decision.ShouldBeOfType<SecurityDenied>();
        denied.Denial.Code.ShouldBe("security.no_policy");
    }
}
