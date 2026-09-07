// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddAgentPermissions_WhenCalledTwice_RegistersOneReplaceableGrantStore()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentPermissions();
        _ = services.AddAgentPermissions();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetServices<ISecurityGrantStore>().ShouldHaveSingleItem().ShouldBeOfType<InMemorySecurityGrantStore>();
        _ = provider.GetServices<ISecurityAuthority>().ShouldHaveSingleItem().ShouldBeOfType<SecurityAuthority>();
        provider.GetRequiredService<TimeProvider>().ShouldBe(TimeProvider.System);
    }
}
