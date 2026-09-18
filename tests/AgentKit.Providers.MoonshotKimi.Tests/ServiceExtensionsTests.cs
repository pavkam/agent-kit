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

    [Theory]
    [InlineData("https://evil.example.test/chat")]
    [InlineData("//evil.example.test/chat")]
    [InlineData("/chat/completions")]
    public void AddMoonshotKimi_WhenChatPathCanReplaceConfiguredEndpoint_FailsStartupValidation(string path)
    {
        var services = new ServiceCollection();
        _ = services.AddMoonshotKimi(options => options.ChatCompletionsPath = path);
        using var provider = services.BuildServiceProvider();
        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
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

    [Fact]
    public void AddMoonshotKimiLlmModel_WhenGivenADescriptor_RegistersAnAdapterServingThatExactDescriptor()
    {
        var services = new ServiceCollection();
        _ = services.AddMoonshotKimi();
        _ = services.AddMoonshotKimiApiKeyCredential("test-key");
        var descriptor = new ModelDescriptor(
            new ModelAlias("exact"),
            MoonshotKimiProviderDefaults.ProviderId,
            MoonshotKimiProviderDefaults.ApiFamily,
            new ModelId("kimi-k2.6"),
            deploymentId: null,
            MoonshotKimiProviderDefaults.DefaultCapabilities,
            MoonshotKimiProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

        _ = services.AddMoonshotKimiLlmModel(descriptor);

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<MoonshotKimiLlmModel>();
        model.Alias.ShouldBe(descriptor.Alias);
    }

    [Fact]
    public void AddMoonshotKimiLlmModel_WhenDescriptorIsNull_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddMoonshotKimiLlmModel(null!)).ParamName.ShouldBe("descriptor");
    }

    [Fact]
    public void AddMoonshotKimiLlmModel_WhenDescriptorNamesAnotherProvider_ThrowsArgumentExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();
        var foreign = new ModelDescriptor(
            new ModelAlias("foreign"),
            new ProviderId("someone-else"),
            MoonshotKimiProviderDefaults.ApiFamily,
            new ModelId("kimi-k2.6"),
            deploymentId: null,
            MoonshotKimiProviderDefaults.DefaultCapabilities,
            MoonshotKimiProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

        var exception = Should.Throw<ArgumentException>(() => services.AddMoonshotKimiLlmModel(foreign));

        exception.ParamName.ShouldBe("descriptor");
        services.ShouldBeEmpty();
    }

    [Fact]
    public async Task AddMoonshotKimiKnownLlmModel_WhenModelIsKnown_RegistersAdapterAndIdenticalCatalogDescriptor()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentProviders();
        _ = services.AddMoonshotKimi();
        _ = services.AddMoonshotKimiApiKeyCredential("test-key");

        _ = services.AddMoonshotKimiKnownLlmModel(new ModelAlias("known"), new ModelId("kimi-k2.6"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<MoonshotKimiLlmModel>();
        var snapshot = await provider.GetRequiredService<IModelCatalog>().GetSnapshotAsync(TestContext.Current.CancellationToken);
        var published = snapshot.ConversationModels.ShouldHaveSingleItem();
        published.Alias.ShouldBe(new ModelAlias("known"));
        published.ProviderId.ShouldBe(MoonshotKimiProviderDefaults.ProviderId);
        published.ApiFamily.ShouldBe(MoonshotKimiProviderDefaults.ApiFamily);
        published.ModelId.ShouldBe(new ModelId("kimi-k2.6"));
        _ = published.Limits.MaxContextTokens.ShouldNotBeNull();
        model.Alias.ShouldBe(published.Alias);
        provider.GetRequiredService<ILlmModelResolver>().Resolve(published).ShouldBeSameAs(model);
    }

    [Fact]
    public void AddMoonshotKimiKnownLlmModel_WhenModelIsUnknown_ThrowsArgumentExceptionForModelIdBeforeRegistering()
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<ArgumentException>(() => services.AddMoonshotKimiKnownLlmModel(new ModelAlias("x"), new ModelId("no-such-model")));

        exception.ParamName.ShouldBe("modelId");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddMoonshotKimiKnownLlmModel_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddMoonshotKimiKnownLlmModel(new ModelAlias("x"), new ModelId("kimi-k2.6"))).ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddMoonshotKimiKnownLlmModel_WhenAliasIsDefault_ThrowsArgumentExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddMoonshotKimiKnownLlmModel(default, new ModelId("kimi-k2.6"))).ParamName.ShouldBe("alias");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddMoonshotKimiKnownLlmModel_WhenModelIdIsDefault_ThrowsArgumentExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddMoonshotKimiKnownLlmModel(new ModelAlias("x"), default)).ParamName.ShouldBe("modelId");
        services.ShouldBeEmpty();
    }

    private sealed class StaticOAuthTokenProviderRegistration: IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new OAuthTokenProviderCredential("token", null));
    }
}
