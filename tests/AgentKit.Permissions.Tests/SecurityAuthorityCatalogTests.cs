// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="SecurityAuthorityCatalog"/> behavior.</summary>
public sealed class SecurityAuthorityCatalogTests
{
    [Fact]
    public void Contains_WhenBindingWasInstalled_ReturnsTrueOnlyForThatKey()
    {
        var installed = new ComponentKey<ISecurityAuthority>("installed");
        var catalog = new SecurityAuthorityCatalog([new SecurityAuthorityKeyRegistration(installed)]);

        catalog.Contains(installed).ShouldBeTrue();
        catalog.Contains(new ComponentKey<ISecurityAuthority>("other")).ShouldBeFalse();
    }

    [Fact]
    public void Contains_WhenNoBindingsExist_ReturnsFalse() =>
        new SecurityAuthorityCatalog([]).Contains(new ComponentKey<ISecurityAuthority>("any")).ShouldBeFalse();

    [Fact]
    public void Contains_WhenKeyIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new SecurityAuthorityCatalog([]).Contains(default)).ParamName.ShouldBe("key");

    [Fact]
    public void Constructor_WhenBindingsIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new SecurityAuthorityCatalog(null!)).ParamName.ShouldBe("registrations");

    [Fact]
    public void AddAgentPermissions_WhenAuthorityIsBound_PublishesACatalogReportingIt()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentPermissions(static options => options.PolicySnapshot = TestSecurityEvidence.PolicySnapshot);
        _ = services.AddSecurityAuthority(new ComponentKey<ISecurityAuthority>("bound"), new UninvokedSecurityAuthority());

        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<ISecurityAuthorityCatalog>();

        catalog.Contains(new ComponentKey<ISecurityAuthority>("bound")).ShouldBeTrue();
    }

    [Fact]
    public void AddSecurityAuthority_WhenBoundByKeyAlone_ReportsTheKeyWithoutActivatingTheAuthority()
    {
        var activations = 0;
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityAuthority>(_ =>
        {
            activations++;
            return new UninvokedSecurityAuthority();
        });
        _ = services.AddSecurityAuthority(new ComponentKey<ISecurityAuthority>("lazy"));
        _ = services.AddAgentPermissions(static options => options.PolicySnapshot = TestSecurityEvidence.PolicySnapshot);

        using var provider = services.BuildServiceProvider();
        var contained = provider.GetRequiredService<ISecurityAuthorityCatalog>().Contains(new ComponentKey<ISecurityAuthority>("lazy"));

        contained.ShouldBeTrue();
        activations.ShouldBe(0);
    }
}
