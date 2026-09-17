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

    [Fact]
    public void ReplaceDelegatedIdentityDeriver_WhenCalled_ReplacesTheSingularDeriver()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentIdentity();
        _ = services.ReplaceDelegatedIdentityDeriver<StubDelegatedIdentityDeriver>();
        using var provider = services.BuildServiceProvider();

        var deriver = provider.GetRequiredService<IDelegatedIdentityDeriver>();

        _ = deriver.ShouldBeOfType<StubDelegatedIdentityDeriver>();
        provider.GetServices<IDelegatedIdentityDeriver>().Count().ShouldBe(1);
    }

    [Fact]
    public void ReplaceIdentityResolver_WhenCalled_ReplacesTheScopedResolver()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentIdentity();
        _ = services.ReplaceIdentityResolver<StubExecutionIdentityResolver>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var resolver = scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>();

        _ = resolver.ShouldBeOfType<StubExecutionIdentityResolver>();
        scope.ServiceProvider.GetServices<IExecutionIdentityResolver>().Count().ShouldBe(1);
    }

    private sealed class StubDelegatedIdentityDeriver: IDelegatedIdentityDeriver
    {
        public ValueTask<IdentityResolutionResult> DeriveAsync(DelegatedIdentityRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not invoked by this test.");
    }

    private sealed class StubExecutionIdentityResolver: IExecutionIdentityResolver
    {
        public ValueTask<IdentityResolutionResult> ResolveAsync(IdentityAssertion assertion, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not invoked by this test.");
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

    [Fact]
    public void AddAgentIdentity_WhenOptionsAreInvalid_FailsStartupValidationBeforeFirstUse()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentIdentity(options => options.MaximumDelegationDepth = 0);
        using var provider = services.BuildServiceProvider();

        // IStartupValidator is what a generic-host application resolves and invokes automatically
        // during IHost.StartAsync() for every option type registered with ValidateOnStart(); calling
        // it directly here reproduces that host behavior without depending on the hosting package.
        // Without ValidateOnStart(), AgentIdentityOptions would not be registered against it at all,
        // so misconfiguration would only surface much later, on first identity use.
        var startupValidator = provider.GetRequiredService<IStartupValidator>();
        _ = Should.Throw<OptionsValidationException>(startupValidator.Validate);
    }
}
