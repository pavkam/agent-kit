// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.DeepSeek.Tests;

using System.Diagnostics;

using AgentKit.Providers.Groq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddDeepSeek_WhenNoOptionsConfigured_RegistersDefaultOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddDeepSeek();
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<DeepSeekProviderOptions>>().Value;
        options.BaseAddress.ShouldBe(DeepSeekProviderDefaults.DefaultBaseAddress);
        options.ChatCompletionsPath.ShouldBe(DeepSeekProviderDefaults.DefaultChatCompletionsPath);
    }

    [Fact]
    public void AddDeepSeek_WhenBaseAddressIsNotAbsolute_FailsStartupValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddDeepSeek(options => options.BaseAddress = new Uri("not-absolute", UriKind.Relative));
        using var provider = services.BuildServiceProvider();
        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Theory]
    [InlineData("https://evil.example.test/chat")]
    [InlineData("//evil.example.test/chat")]
    [InlineData("/chat/completions")]
    public void AddDeepSeek_WhenChatPathCanReplaceConfiguredEndpoint_FailsStartupValidation(string path)
    {
        var services = new ServiceCollection();
        _ = services.AddDeepSeek(options => options.ChatCompletionsPath = path);
        using var provider = services.BuildServiceProvider();
        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void AddDeepSeekApiKeyCredential_WhenRegistered_ResolvesApiKeyCredential()
    {
        var services = new ServiceCollection();
        _ = services.AddDeepSeek();
        _ = services.AddDeepSeekApiKeyCredential("ds-test-key");
        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(DeepSeekProviderDefaults.ProviderId);
        _ = source.ShouldBeOfType<StaticApiKeyCredentialSource>();
    }

    [Fact]
    public void AddDeepSeekOAuthCredential_WhenRegistered_ResolvesDelegatingOAuthSource()
    {
        var services = new ServiceCollection();
        _ = services.AddDeepSeek();
        _ = services.AddDeepSeekOAuthCredential<StaticOAuthTokenProviderRegistration>();
        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(DeepSeekProviderDefaults.ProviderId);
        _ = source.ShouldBeOfType<DelegatingOAuthCredentialSource>();
    }

    [Fact]
    public void AddDeepSeekLlmModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddDeepSeek();
        _ = services.AddDeepSeekApiKeyCredential("ds-test-key");
        _ = services.AddDeepSeekLlmModel(new ModelAlias("chat"), new ModelId("deepseek-chat"));
        _ = services.AddDeepSeekLlmModel(new ModelAlias("reasoner"), new ModelId("deepseek-reasoner"));
        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<ILlmModel>().ToArray();
        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["chat", "reasoner"], ignoreOrder: true);
    }

    [Fact]
    public void AddDeepSeekLlmModel_WhenResolved_UsesDeepSeekProviderIdentity()
    {
        var services = new ServiceCollection();
        _ = services.AddDeepSeek();
        _ = services.AddDeepSeekApiKeyCredential("ds-test-key");
        _ = services.AddDeepSeekLlmModel(new ModelAlias("chat"), new ModelId("deepseek-chat"));
        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<DeepSeekLlmModel>();
        model.Alias.ShouldBe(new ModelAlias("chat"));
    }

    private sealed class StaticOAuthTokenProviderRegistration: IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(new OAuthTokenProviderCredential("token", null));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddProviderCredentials_WhenDeepSeekAndGroqCoexist_ResolvesByProviderIdentityRegardlessOfOrder(bool registerDeepSeekFirst)
    {
        var services = new ServiceCollection();
        if (registerDeepSeekFirst)
        {
            RegisterDeepSeek(services);
            RegisterGroq(services);
        }
        else
        {
            RegisterGroq(services);
            RegisterDeepSeek(services);
        }

        await using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<ILlmModel>().ToArray();
        models.Select(model => model.Alias.Value).ShouldBe(["deepseek-chat", "groq-chat"], ignoreOrder: true);
        var deepSeekCredential = await ResolveApiKeyAsync(provider, DeepSeekProviderDefaults.ProviderId);
        var groqCredential = await ResolveApiKeyAsync(provider, GroqProviderDefaults.ProviderId);
        deepSeekCredential.ApiKey.ShouldBe("deepseek-key");
        groqCredential.ApiKey.ShouldBe("groq-key");
    }

    private static void RegisterDeepSeek(IServiceCollection services)
    {
        Debug.Assert(services is not null, "The caller must provide a service collection.");
        _ = services.AddDeepSeek();
        _ = services.AddDeepSeekApiKeyCredential("deepseek-key");
        _ = services.AddDeepSeekLlmModel(new ModelAlias("deepseek-chat"), new ModelId("deepseek-chat"));
    }

    private static void RegisterGroq(IServiceCollection services)
    {
        Debug.Assert(services is not null, "The caller must provide a service collection.");
        _ = services.AddGroq();
        _ = services.AddGroqApiKeyCredential("groq-key");
        _ = services.AddGroqLlmModel(new ModelAlias("groq-chat"), new ModelId("llama-3.3-70b-versatile"));
    }

    [Fact]
    public void AddDeepSeekLlmModel_WhenGivenADescriptor_RegistersAnAdapterServingThatExactDescriptor()
    {
        var services = new ServiceCollection();
        _ = services.AddDeepSeek();
        _ = services.AddDeepSeekApiKeyCredential("ds-test-key");
        var descriptor = new ModelDescriptor(
            new ModelAlias("exact"),
            DeepSeekProviderDefaults.ProviderId,
            DeepSeekProviderDefaults.ApiFamily,
            new ModelId("deepseek-flash"),
            deploymentId: null,
            DeepSeekProviderDefaults.DefaultCapabilities,
            DeepSeekProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

        _ = services.AddDeepSeekLlmModel(descriptor);

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<DeepSeekLlmModel>();
        model.Alias.ShouldBe(descriptor.Alias);
    }

    [Fact]
    public void AddDeepSeekLlmModel_WhenDescriptorIsNull_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddDeepSeekLlmModel(null!)).ParamName.ShouldBe("descriptor");
    }

    [Fact]
    public void AddDeepSeekLlmModel_WhenDescriptorNamesAnotherProvider_ThrowsArgumentExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();
        var foreign = new ModelDescriptor(
            new ModelAlias("foreign"),
            new ProviderId("someone-else"),
            DeepSeekProviderDefaults.ApiFamily,
            new ModelId("deepseek-flash"),
            deploymentId: null,
            DeepSeekProviderDefaults.DefaultCapabilities,
            DeepSeekProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

        var exception = Should.Throw<ArgumentException>(() => services.AddDeepSeekLlmModel(foreign));

        exception.ParamName.ShouldBe("descriptor");
        services.ShouldBeEmpty();
    }

    [Fact]
    public async Task AddDeepSeekKnownLlmModel_WhenModelIsKnown_RegistersAdapterAndIdenticalCatalogDescriptor()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentProviders();
        _ = services.AddDeepSeek();
        _ = services.AddDeepSeekApiKeyCredential("ds-test-key");

        _ = services.AddDeepSeekKnownLlmModel(new ModelAlias("known"), new ModelId("deepseek-flash"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<DeepSeekLlmModel>();
        var snapshot = await provider.GetRequiredService<IModelCatalog>().GetSnapshotAsync(TestContext.Current.CancellationToken);
        var published = snapshot.ConversationModels.ShouldHaveSingleItem();
        published.Alias.ShouldBe(new ModelAlias("known"));
        published.ProviderId.ShouldBe(DeepSeekProviderDefaults.ProviderId);
        published.ApiFamily.ShouldBe(DeepSeekProviderDefaults.ApiFamily);
        published.ModelId.ShouldBe(new ModelId("deepseek-flash"));
        _ = published.Limits.MaxContextTokens.ShouldNotBeNull();
        model.Alias.ShouldBe(published.Alias);
        provider.GetRequiredService<ILlmModelResolver>().Resolve(published).ShouldBeSameAs(model);
    }

    [Fact]
    public void AddDeepSeekKnownLlmModel_WhenModelIsUnknown_ThrowsArgumentExceptionForModelIdBeforeRegistering()
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<ArgumentException>(() => services.AddDeepSeekKnownLlmModel(new ModelAlias("x"), new ModelId("no-such-model")));

        exception.ParamName.ShouldBe("modelId");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddDeepSeekKnownLlmModel_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddDeepSeekKnownLlmModel(new ModelAlias("x"), new ModelId("deepseek-flash"))).ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddDeepSeekKnownLlmModel_WhenAliasIsDefault_ThrowsArgumentExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddDeepSeekKnownLlmModel(default, new ModelId("deepseek-flash"))).ParamName.ShouldBe("alias");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddDeepSeekKnownLlmModel_WhenModelIdIsDefault_ThrowsArgumentExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddDeepSeekKnownLlmModel(new ModelAlias("x"), default)).ParamName.ShouldBe("modelId");
        services.ShouldBeEmpty();
    }

    private static async ValueTask<ApiKeyProviderCredential> ResolveApiKeyAsync(IServiceProvider provider, ProviderId providerId)
    {
        Debug.Assert(provider is not null, "The caller must provide a service provider.");
        Debug.Assert(!string.IsNullOrWhiteSpace(providerId.Value), "The caller must provide a valid provider identity.");
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(providerId);
        var credential = await source.GetCredentialAsync(providerId, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return credential.ShouldBeOfType<ApiKeyProviderCredential>();
    }
}
