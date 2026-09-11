// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;



/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddAgentIdentity_WhenCalledTwice_KeepsSingularRuntimeServices()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentIdentity();
        _ = services.AddAgentIdentity();
        using var provider = services.BuildServiceProvider();
        provider.GetServices<IExecutionIdentityResolver>().Count().ShouldBe(1);
        provider.GetServices<IDelegatedIdentityDeriver>().Count().ShouldBe(1);
    }

    [Fact]
    public void AddIdentityIssuer_WhenKeyIsDuplicated_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentIdentity();
        _ = services.AddIdentityIssuer<TestIssuer>(new IdentityIssuerRegistration(new IdentityIssuerId("issuer")));
        _ = Should.Throw<InvalidOperationException>(() => services.AddIdentityIssuer<TestIssuer>(new IdentityIssuerRegistration(new IdentityIssuerId("issuer"))));
    }

    [Fact]
    public void AddIdentityNormalizationPolicy_WhenNameIsDuplicated_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentIdentity();
        _ = services.AddIdentityNormalizationPolicy<NullPolicy>(new IdentityNormalizationPolicyRegistration("duplicate"));
        _ = Should.Throw<InvalidOperationException>(() => services.AddIdentityNormalizationPolicy<NullPolicy>(new IdentityNormalizationPolicyRegistration("duplicate")));
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, -1)]
    public void AddAgentIdentity_WhenOptionsAreInvalid_FailsValidationOnResolution(int depth, int lifetimeHours, int skewMinutes)
    {
        var services = new ServiceCollection();
        _ = services.AddAgentIdentity(options =>
        {
            options.MaximumDelegationDepth = depth;
            options.MaximumEvidenceLifetime = TimeSpan.FromHours(lifetimeHours);
            options.MaximumClockSkew = TimeSpan.FromMinutes(skewMinutes);
        });
        using var provider = services.BuildServiceProvider();
        _ = Should.Throw<OptionsValidationException>(provider.GetRequiredService<IDelegatedIdentityDeriver>);
    }
}
