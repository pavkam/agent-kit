// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

public sealed class DenyAllSecurityAuthorityTests
{
    [Fact]
    public async Task AuthorizeAsync_WhenRequestIsValid_DeniesWithoutGrant()
    {
        var authority = new DenyAllSecurityAuthority();
        var request = SecurityAuthorityTestData.CreateRequest();

        var decision = await authority.AuthorizeAsync(request, TestContext.Current.CancellationToken);

        var denied = decision.ShouldBeOfType<SecurityDenied>();
        denied.Denial.Code.ShouldBe("security.no_policy");
    }
}
