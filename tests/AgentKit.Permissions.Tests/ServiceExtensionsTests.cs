// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

/// <summary>Verifies registration of the security runtime independently from storage-adapter selection.</summary>
public sealed class ServiceExtensionsTests
{
    /// <summary>Verifies the core registration is idempotent and leaves grant storage to an explicit adapter.</summary>
    [Fact]
    public void AddAgentPermissions_WhenCalledTwice_RegistersOneRuntimeAndNoGrantStore()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentPermissions();
        _ = services.AddAgentPermissions();
        using var provider = services.BuildServiceProvider();

        provider.GetServices<ISecurityGrantStore>().ShouldBeEmpty();
        services.Count(static descriptor => descriptor.ServiceType == typeof(ISecurityAuthority)).ShouldBe(1);
        provider.GetRequiredService<TimeProvider>().ShouldBe(TimeProvider.System);
    }

    /// <summary>Verifies selecting the explicit in-memory adapter completes the core authority composition.</summary>
    [Fact]
    public void AddAgentPermissions_WhenExplicitInMemoryStoreIsSelected_ResolvesIssuingAuthority()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentPermissions();
        _ = services.AddInMemorySecurityGrantStore();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<ISecurityAuthority>().ShouldBeOfType<SecurityAuthority>();
    }

    /// <summary>Verifies incomplete security composition fails while resolving the issuing authority before any protected operation.</summary>
    [Fact]
    public void AddAgentPermissions_WhenNoGrantStoreIsSelected_FailsBeforeAuthorityCanIssueGrants()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentPermissions();
        using var provider = services.BuildServiceProvider();

        var exception = Should.Throw<InvalidOperationException>(provider.GetRequiredService<ISecurityAuthority>);

        exception.Message.ShouldContain(nameof(ISecurityGrantStore));
    }
}
