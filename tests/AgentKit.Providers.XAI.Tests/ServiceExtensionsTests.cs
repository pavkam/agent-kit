// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.XAI.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Verifies the <c>AddXAI*</c> dependency-injection registration surface:
/// endpoint options, credential source selection, and additive model
/// registration.
/// </summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddXAI_WhenNoOptionsConfigured_RegistersDefaultOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddXAI();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XAIProviderOptions>>().Value;

        options.BaseAddress.ShouldBe(XAIProviderDefaults.DefaultBaseAddress);
        options.ChatCompletionsPath.ShouldBe(XAIProviderDefaults.DefaultChatCompletionsPath);
    }

    [Fact]
    public void AddXAI_WhenBaseAddressIsNotAbsolute_FailsStartupValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddXAI(options => options.BaseAddress = new Uri("not-absolute", UriKind.Relative));

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Theory]
    [InlineData("https://evil.example.test/chat")]
    [InlineData("//evil.example.test/chat")]
    [InlineData("/chat/completions")]
    public void AddXAI_WhenChatPathCanReplaceConfiguredEndpoint_FailsStartupValidation(string path)
    {
        var services = new ServiceCollection();
        _ = services.AddXAI(options => options.ChatCompletionsPath = path);
        using var provider = services.BuildServiceProvider();
        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Theory]
    [InlineData("https://evil.example.test/embeddings")]
    [InlineData("/embeddings")]
    public void AddXAI_WhenEmbeddingsPathCanReplaceConfiguredEndpoint_FailsStartupValidation(string path)
    {
        var services = new ServiceCollection();
        _ = services.AddXAI(options => options.EmbeddingsPath = path);
        using var provider = services.BuildServiceProvider();
        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void AddXAIApiKeyCredential_WhenRegistered_ResolvesApiKeyCredential()
    {
        var services = new ServiceCollection();
        _ = services.AddXAI();
        _ = services.AddXAIApiKeyCredential("test-key");

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(
            XAIProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<StaticApiKeyCredentialSource>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddXAIApiKeyCredential_WhenAnotherProviderCredentialCoexists_ResolvesByProviderIdentity(
        bool xAiRegisteredFirst)
    {
        var services = new ServiceCollection();
        var otherProviderId = new ProviderId("other-provider");
        var otherSource = new StaticApiKeyCredentialSource("other-key");

        if (xAiRegisteredFirst)
        {
            _ = services.AddXAIApiKeyCredential("xai-key");
            _ = services.AddKeyedSingleton<IProviderCredentialSource>(otherProviderId, otherSource);
        }
        else
        {
            _ = services.AddKeyedSingleton<IProviderCredentialSource>(otherProviderId, otherSource);
            _ = services.AddXAIApiKeyCredential("xai-key");
        }

        await using var provider = services.BuildServiceProvider();
        var xAiSource = provider.GetRequiredKeyedService<IProviderCredentialSource>(
            XAIProviderDefaults.ProviderId);
        var resolvedOtherSource = provider.GetRequiredKeyedService<IProviderCredentialSource>(otherProviderId);

        var xAiCredential = await xAiSource.GetCredentialAsync(
            XAIProviderDefaults.ProviderId,
            TestContext.Current.CancellationToken);
        var otherCredential = await resolvedOtherSource.GetCredentialAsync(
            otherProviderId,
            TestContext.Current.CancellationToken);

        xAiCredential.ShouldBeOfType<ApiKeyProviderCredential>().ApiKey.ShouldBe("xai-key");
        otherCredential.ShouldBeOfType<ApiKeyProviderCredential>().ApiKey.ShouldBe("other-key");
    }

    [Fact]
    public void AddXAIOAuthCredential_WhenRegistered_ResolvesDelegatingOAuthSource()
    {
        var services = new ServiceCollection();
        _ = services.AddXAI();
        _ = services.AddXAIOAuthCredential<StaticOAuthTokenProviderRegistration>();

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(
            XAIProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<DelegatingOAuthCredentialSource>();
    }

    [Fact]
    public void AddXAILlmModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddXAI();
        _ = services.AddXAIApiKeyCredential("test-key");
        _ = services.AddXAILlmModel(new ModelAlias("primary"), new ModelId("grok-4"));
        _ = services.AddXAILlmModel(new ModelAlias("secondary"), new ModelId("grok-4-fast"));

        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<ILlmModel>().ToArray();

        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["primary", "secondary"], ignoreOrder: true);
    }

    [Fact]
    public void AddXAILlmModel_WhenResolved_UsesXAIProviderIdentity()
    {
        var services = new ServiceCollection();
        _ = services.AddXAI();
        _ = services.AddXAIApiKeyCredential("test-key");
        _ = services.AddXAILlmModel(new ModelAlias("chat"), new ModelId("grok-4"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<XAILlmModel>();

        model.Alias.ShouldBe(new ModelAlias("chat"));
    }

    [Fact]
    public void AddXAIEmbeddingModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddXAI();
        _ = services.AddXAIApiKeyCredential("test-key");
        _ = services.AddXAIEmbeddingModel(new EmbeddingModelAlias("primary"), new ModelId("xai-embed-1"));
        _ = services.AddXAIEmbeddingModel(new EmbeddingModelAlias("secondary"), new ModelId("xai-embed-2"));

        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<IEmbeddingModel>().ToArray();

        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["primary", "secondary"], ignoreOrder: true);
    }

    [Fact]
    public void AddXAIEmbeddingModel_WhenResolved_UsesXAIEmbeddingModelType()
    {
        var services = new ServiceCollection();
        _ = services.AddXAI();
        _ = services.AddXAIApiKeyCredential("test-key");
        _ = services.AddXAIEmbeddingModel(new EmbeddingModelAlias("embed"), new ModelId("xai-embed-1"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<IEmbeddingModel>().ShouldBeOfType<XAIEmbeddingModel>();

        model.Alias.ShouldBe(new EmbeddingModelAlias("embed"));
    }

    [Fact]
    public void AddXAILlmModel_WhenGivenADescriptor_RegistersAnAdapterServingThatExactDescriptor()
    {
        var services = new ServiceCollection();
        _ = services.AddXAI();
        _ = services.AddXAIApiKeyCredential("test-key");
        var descriptor = new ModelDescriptor(
            new ModelAlias("exact"),
            XAIProviderDefaults.ProviderId,
            XAIProviderDefaults.ApiFamily,
            new ModelId("grok-4.20-0309-non-reasoning"),
            deploymentId: null,
            XAIProviderDefaults.DefaultCapabilities,
            XAIProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

        _ = services.AddXAILlmModel(descriptor);

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<XAILlmModel>();
        model.Alias.ShouldBe(descriptor.Alias);
    }

    [Fact]
    public void AddXAILlmModel_WhenDescriptorIsNull_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddXAILlmModel(null!)).ParamName.ShouldBe("descriptor");
    }

    [Fact]
    public void AddXAILlmModel_WhenDescriptorNamesAnotherProvider_ThrowsArgumentExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();
        var foreign = new ModelDescriptor(
            new ModelAlias("foreign"),
            new ProviderId("someone-else"),
            XAIProviderDefaults.ApiFamily,
            new ModelId("grok-4.20-0309-non-reasoning"),
            deploymentId: null,
            XAIProviderDefaults.DefaultCapabilities,
            XAIProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

        var exception = Should.Throw<ArgumentException>(() => services.AddXAILlmModel(foreign));

        exception.ParamName.ShouldBe("descriptor");
        services.ShouldBeEmpty();
    }

    [Fact]
    public async Task AddXAIKnownLlmModel_WhenModelIsKnown_RegistersAdapterAndIdenticalCatalogDescriptor()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentProviders();
        _ = services.AddXAI();
        _ = services.AddXAIApiKeyCredential("test-key");

        _ = services.AddXAIKnownLlmModel(new ModelAlias("known"), new ModelId("grok-4.20-0309-non-reasoning"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<XAILlmModel>();
        var snapshot = await provider.GetRequiredService<IModelCatalog>().GetSnapshotAsync(TestContext.Current.CancellationToken);
        var published = snapshot.ConversationModels.ShouldHaveSingleItem();
        published.Alias.ShouldBe(new ModelAlias("known"));
        published.ProviderId.ShouldBe(XAIProviderDefaults.ProviderId);
        published.ApiFamily.ShouldBe(XAIProviderDefaults.ApiFamily);
        published.ModelId.ShouldBe(new ModelId("grok-4.20-0309-non-reasoning"));
        _ = published.Limits.MaxContextTokens.ShouldNotBeNull();
        model.Alias.ShouldBe(published.Alias);
        provider.GetRequiredService<ILlmModelResolver>().Resolve(published).ShouldBeSameAs(model);
    }

    [Fact]
    public void AddXAIKnownLlmModel_WhenModelIsUnknown_ThrowsArgumentExceptionForModelIdBeforeRegistering()
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<ArgumentException>(() => services.AddXAIKnownLlmModel(new ModelAlias("x"), new ModelId("no-such-model")));

        exception.ParamName.ShouldBe("modelId");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddXAIKnownLlmModel_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddXAIKnownLlmModel(new ModelAlias("x"), new ModelId("grok-4.20-0309-non-reasoning"))).ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddXAIKnownLlmModel_WhenAliasIsDefault_ThrowsArgumentExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddXAIKnownLlmModel(default, new ModelId("grok-4.20-0309-non-reasoning"))).ParamName.ShouldBe("alias");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddXAIKnownLlmModel_WhenModelIdIsDefault_ThrowsArgumentExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddXAIKnownLlmModel(new ModelAlias("x"), default)).ParamName.ShouldBe("modelId");
        services.ShouldBeEmpty();
    }

    private sealed class StaticOAuthTokenProviderRegistration: IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new OAuthTokenProviderCredential("token", null));
    }
}
