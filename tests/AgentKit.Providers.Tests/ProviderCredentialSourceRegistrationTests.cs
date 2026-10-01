// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests;

using AgentKit.Providers.Credentials;
using AgentKit.TestSupport;

/// <summary>Verifies that <see cref="ProviderCredentialSourceRegistration"/> registers key-only, grant-enforcing sources.</summary>
public sealed class ProviderCredentialSourceRegistrationTests
{
    private static readonly ProviderCredentialSourceKey SourceKey = new("registered.source");

    private static readonly ProviderId ProviderIdentity = new("registered-provider");

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        _ = services.AddProviderEgressTestServices();
        return services;
    }

    [Fact]
    public async Task AddStaticApiKeySource_WhenRegistered_ReleasesTheKeyOnlyUnderTheSourceKey()
    {
        var services = CreateServices();
        _ = ProviderCredentialSourceRegistration.AddStaticApiKeySource(services, SourceKey, "registered-key");
        await using var provider = services.BuildServiceProvider();

        var probe = await ProviderCredentialProbe.ProbeAsync(provider, SourceKey, cancellationToken: TestContext.Current.CancellationToken);

        probe.Headers["Authorization"].ShouldBe("Bearer registered-key");
        provider.GetRequiredKeyedService<IProviderCredentialSource>(SourceKey).ShouldBeOfType<StaticApiKeyCredentialSource>().Key.ShouldBe(SourceKey);
        provider.GetKeyedService<IProviderCredentialSource>(ProviderIdentity).ShouldBeNull();
    }

    [Fact]
    public async Task AddStaticApiKeySource_WhenKeyIsAlreadyRegistered_KeepsTheFirstRegistration()
    {
        var services = CreateServices();
        _ = ProviderCredentialSourceRegistration.AddStaticApiKeySource(services, SourceKey, "first-key");
        _ = ProviderCredentialSourceRegistration.AddStaticApiKeySource(services, SourceKey, "second-key");
        await using var provider = services.BuildServiceProvider();

        var probe = await ProviderCredentialProbe.ProbeAsync(provider, SourceKey, cancellationToken: TestContext.Current.CancellationToken);

        probe.Headers["Authorization"].ShouldBe("Bearer first-key");
    }

    [Fact]
    public async Task AddStaticApiKeySource_WhenTwoSourcesCoexist_EachKeyReleasesItsOwnCredential()
    {
        var services = CreateServices();
        var other = new ProviderCredentialSourceKey("other.source");
        _ = ProviderCredentialSourceRegistration.AddStaticApiKeySource(services, SourceKey, "key-a");
        _ = ProviderCredentialSourceRegistration.AddStaticApiKeySource(services, other, "key-b");
        await using var provider = services.BuildServiceProvider();

        var first = await ProviderCredentialProbe.ProbeAsync(provider, SourceKey, cancellationToken: TestContext.Current.CancellationToken);
        var second = await ProviderCredentialProbe.ProbeAsync(provider, other, cancellationToken: TestContext.Current.CancellationToken);

        first.Headers["Authorization"].ShouldBe("Bearer key-a");
        second.Headers["Authorization"].ShouldBe("Bearer key-b");
    }

    [Fact]
    public async Task AddOAuthTokenSource_WhenRegistered_ReleasesTheProvidersTokenOnlyUnderTheSourceKey()
    {
        var services = CreateServices();
        _ = ProviderCredentialSourceRegistration.AddOAuthTokenSource<TestTokenProvider>(services, SourceKey, ProviderIdentity);
        await using var provider = services.BuildServiceProvider();

        var probe = await ProviderCredentialProbe.ProbeAsync(provider, SourceKey, cancellationToken: TestContext.Current.CancellationToken);

        probe.Headers["Authorization"].ShouldBe("Bearer registered-token");
        _ = provider.GetRequiredKeyedService<IProviderCredentialSource>(SourceKey).ShouldBeOfType<DelegatingOAuthCredentialSource>();
        provider.GetKeyedService<IProviderCredentialSource>(ProviderIdentity).ShouldBeNull();
        _ = provider.GetRequiredKeyedService<IOAuthAccessTokenProvider>(ProviderIdentity).ShouldBeOfType<TestTokenProvider>();
    }

    [Fact]
    public async Task AddOAuthTokenSource_WhenStaticSourceWasRegisteredFirst_DoesNotReplaceIt()
    {
        var services = CreateServices();
        _ = ProviderCredentialSourceRegistration.AddStaticApiKeySource(services, SourceKey, "first-key");
        _ = ProviderCredentialSourceRegistration.AddOAuthTokenSource<TestTokenProvider>(services, SourceKey, ProviderIdentity);
        await using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredKeyedService<IProviderCredentialSource>(SourceKey).ShouldBeOfType<StaticApiKeyCredentialSource>();
    }

    [Fact]
    public async Task ReplaceProviderCredentialSource_WhenAFirstPartySourceExists_ThePipelineObservesTheReplacement()
    {
        var services = CreateServices();
        _ = ProviderCredentialSourceRegistration.AddStaticApiKeySource(services, SourceKey, "original-key");
        _ = services.ReplaceProviderCredentialSource<ReplacementSource>(SourceKey);
        await using var provider = services.BuildServiceProvider();

        var probe = await ProviderCredentialProbe.ProbeAsync(provider, SourceKey, cancellationToken: TestContext.Current.CancellationToken);

        probe.Headers["Authorization"].ShouldBe("Bearer replacement-key");
    }

    [Fact]
    public async Task AddCredentialReadGate_WhenCalledTwice_RegistersOneSharedGate()
    {
        var services = CreateServices();
        _ = ProviderCredentialSourceRegistration.AddCredentialReadGate(services);
        _ = ProviderCredentialSourceRegistration.AddCredentialReadGate(services);
        await using var provider = services.BuildServiceProvider();

        services.Count(static descriptor => descriptor.ServiceType == typeof(ProviderCredentialReadGate)).ShouldBe(1);
        provider.GetRequiredService<ProviderCredentialReadGate>().ShouldBeSameAs(provider.GetRequiredService<ProviderCredentialReadGate>());
    }

    [Fact]
    public async Task AddAgentProviders_WhenComposed_RegistersTheCredentialReadGateForCustomSources()
    {
        var services = CreateServices();
        await using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<ProviderCredentialReadGate>();
    }

    [Fact]
    public void Registration_WhenArgumentIsInvalid_ThrowsTheExactException()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => ProviderCredentialSourceRegistration.AddStaticApiKeySource(null!, SourceKey, "k")).ParamName.ShouldBe("services");
        Should.Throw<ArgumentOutOfRangeException>(() => ProviderCredentialSourceRegistration.AddStaticApiKeySource(services, default, "k")).ParamName.ShouldBe("credentialSourceKey");
        Should.Throw<ArgumentException>(() => ProviderCredentialSourceRegistration.AddStaticApiKeySource(services, SourceKey, " ")).ParamName.ShouldBe("apiKey");
        Should.Throw<ArgumentNullException>(() => ProviderCredentialSourceRegistration.AddOAuthTokenSource<TestTokenProvider>(null!, SourceKey, ProviderIdentity)).ParamName.ShouldBe("services");
        Should.Throw<ArgumentOutOfRangeException>(() => ProviderCredentialSourceRegistration.AddOAuthTokenSource<TestTokenProvider>(services, default, ProviderIdentity)).ParamName.ShouldBe("credentialSourceKey");
        Should.Throw<ArgumentOutOfRangeException>(() => ProviderCredentialSourceRegistration.AddOAuthTokenSource<TestTokenProvider>(services, SourceKey, default)).ParamName.ShouldBe("providerId");
        Should.Throw<ArgumentNullException>(() => ProviderCredentialSourceRegistration.AddCredentialReadGate(null!)).ParamName.ShouldBe("services");
    }

    private sealed class TestTokenProvider: IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new OAuthTokenProviderCredential("registered-token", expiresAtUtc: null));
    }

    private sealed class ReplacementSource(ProviderCredentialReadGate gate): IProviderCredentialSource
    {
        public ProviderCredentialSourceKey Key => SourceKey;

        public ComponentId SecurityAudience => ProviderCredentialReadGate.DefaultAudience;

        public async ValueTask<ProviderCredentialResolutionResult> ResolveAsync(
            ProviderCredentialResolutionRequest request,
            CancellationToken cancellationToken = default) =>
            await gate.ConsumeAsync(this, request, cancellationToken).ConfigureAwait(false) is { } refusal
                ? refusal
                : new ProviderCredentialResolved(new ProviderCredentialLease(new ApiKeyProviderCredential("replacement-key")));
    }
}
