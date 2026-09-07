// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MoonshotKimi.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Verifies the <c>AddMoonshotKimi*</c> dependency-injection registration surface:
/// endpoint options, credential source selection, and additive model
/// registration.
/// </summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddMoonshotKimi_WhenNoOptionsConfigured_RegistersDefaultOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddMoonshotKimi();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MoonshotKimiProviderOptions>>().Value;

        options.BaseAddress.ShouldBe(MoonshotKimiProviderDefaults.DefaultBaseAddress);
        options.ChatCompletionsPath.ShouldBe(MoonshotKimiProviderDefaults.DefaultChatCompletionsPath);
    }

    [Fact]
    public void AddMoonshotKimi_WhenBaseAddressIsNotAbsolute_FailsStartupValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddMoonshotKimi(options => options.BaseAddress = new Uri("not-absolute", UriKind.Relative));

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void AddMoonshotKimiApiKeyCredential_WhenRegistered_ResolvesApiKeyCredential()
    {
        var services = new ServiceCollection();
        _ = services.AddMoonshotKimi();
        _ = services.AddMoonshotKimiApiKeyCredential("test-key");

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(
            MoonshotKimiProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<StaticApiKeyCredentialSource>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddMoonshotKimiApiKeyCredential_WhenAnotherProviderCredentialCoexists_ResolvesByProviderIdentity(
        bool moonshotRegisteredFirst)
    {
        var services = new ServiceCollection();
        var otherProviderId = new ProviderId("other-provider");
        var otherSource = new StaticApiKeyCredentialSource("other-key");

        if (moonshotRegisteredFirst)
        {
            _ = services.AddMoonshotKimiApiKeyCredential("moonshot-key");
            _ = services.AddKeyedSingleton<IProviderCredentialSource>(otherProviderId, otherSource);
        }
        else
        {
            _ = services.AddKeyedSingleton<IProviderCredentialSource>(otherProviderId, otherSource);
            _ = services.AddMoonshotKimiApiKeyCredential("moonshot-key");
        }

        await using var provider = services.BuildServiceProvider();
        var moonshotSource = provider.GetRequiredKeyedService<IProviderCredentialSource>(
            MoonshotKimiProviderDefaults.ProviderId);
        var resolvedOtherSource = provider.GetRequiredKeyedService<IProviderCredentialSource>(otherProviderId);

        var moonshotCredential = await moonshotSource.GetCredentialAsync(
            MoonshotKimiProviderDefaults.ProviderId,
            TestContext.Current.CancellationToken);
        var otherCredential = await resolvedOtherSource.GetCredentialAsync(
            otherProviderId,
            TestContext.Current.CancellationToken);

        moonshotCredential.ShouldBeOfType<ApiKeyProviderCredential>().ApiKey.ShouldBe("moonshot-key");
        otherCredential.ShouldBeOfType<ApiKeyProviderCredential>().ApiKey.ShouldBe("other-key");
    }

    [Fact]
    public void AddMoonshotKimiOAuthCredential_WhenRegistered_ResolvesDelegatingOAuthSource()
    {
        var services = new ServiceCollection();
        _ = services.AddMoonshotKimi();
        _ = services.AddMoonshotKimiOAuthCredential<StaticOAuthTokenProviderRegistration>();

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(
            MoonshotKimiProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<DelegatingOAuthCredentialSource>();
    }

    [Fact]
    public void AddMoonshotKimiLlmModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddMoonshotKimi();
        _ = services.AddMoonshotKimiApiKeyCredential("test-key");
        _ = services.AddMoonshotKimiLlmModel(new ModelAlias("primary"), new ModelId("kimi-k2-0711-preview"));
        _ = services.AddMoonshotKimiLlmModel(new ModelAlias("secondary"), new ModelId("kimi-k1.5"));

        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<ILlmModel>().ToArray();

        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["primary", "secondary"], ignoreOrder: true);
    }

    [Fact]
    public void AddMoonshotKimiLlmModel_WhenResolved_UsesMoonshotKimiProviderIdentity()
    {
        var services = new ServiceCollection();
        _ = services.AddMoonshotKimi();
        _ = services.AddMoonshotKimiApiKeyCredential("test-key");
        _ = services.AddMoonshotKimiLlmModel(new ModelAlias("chat"), new ModelId("kimi-k2-0711-preview"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<MoonshotKimiLlmModel>();

        model.Alias.ShouldBe(new ModelAlias("chat"));
    }

    private sealed class StaticOAuthTokenProviderRegistration: IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new OAuthTokenProviderCredential("token", null));
    }
}
