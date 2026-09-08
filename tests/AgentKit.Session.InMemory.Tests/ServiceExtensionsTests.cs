// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

using Microsoft.Extensions.DependencyInjection;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddInMemorySessionStore_WhenCalled_RegistersOneValidStoreGraph()
    {
        var services = new ServiceCollection();
        var security = new TestSecurityHarness();

        _ = services.AddSingleton<ISecurityGrantStore>(security);
        _ = services.AddSingleton<ISecurityAuditDispatcher>(security);
        _ = services.AddInMemorySessionStore();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        _ = provider.GetRequiredService<ISessionStore>().ShouldBeOfType<InMemorySessionStore>();
        provider.GetServices<ISessionStore>().Count().ShouldBe(1);
        _ = provider.GetRequiredService<IIdentifierGenerator<BranchId>>();
        _ = provider.GetRequiredService<IIdentifierGenerator<SecurityAuditRecordId>>();
        _ = provider.GetRequiredService<TimeProvider>();
    }
}
