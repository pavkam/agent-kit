// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Verifies the <c>AddMistralAI*</c> dependency-injection registration
/// surface: endpoint options, credential source selection, and additive
/// model registration.
/// </summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddMistralAI_WhenNoOptionsConfigured_RegistersDefaultOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddMistralAI();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MistralAIProviderOptions>>().Value;

        options.BaseAddress.ShouldBe(MistralAIProviderDefaults.DefaultBaseAddress);
        options.ChatCompletionsPath.ShouldBe(MistralAIProviderDefaults.DefaultChatCompletionsPath);
        options.PreferStreaming.ShouldBeTrue();
    }

    [Fact]
    public void AddMistralAI_WhenBaseAddressIsNotAbsolute_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddMistralAI(options => options.BaseAddress = new Uri("not-absolute", UriKind.Relative));

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<MistralAIProviderOptions>>().Value);
    }

    [Fact]
    public void AddMistralAI_WhenChatCompletionsPathIsWhitespace_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddMistralAI(options => options.ChatCompletionsPath = "   ");

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<MistralAIProviderOptions>>().Value);
    }

    [Fact]
    public void AddMistralAIApiKeyCredential_WhenRegistered_ResolvesApiKeyCredential()
    {
        var services = new ServiceCollection();
        _ = services.AddMistralAI();
        _ = services.AddMistralAIApiKeyCredential("mistral-test-key");

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(MistralAIProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<StaticApiKeyCredentialSource>();
    }

    [Fact]
    public void AddMistralAIOAuthCredential_WhenRegistered_ResolvesDelegatingOAuthSource()
    {
        var services = new ServiceCollection();
        _ = services.AddMistralAI();
        _ = services.AddMistralAIOAuthCredential<StaticOAuthTokenProviderRegistration>();

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(MistralAIProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<DelegatingOAuthCredentialSource>();
    }

    [Fact]
    public void AddMistralAILlmModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddMistralAI();
        _ = services.AddMistralAIApiKeyCredential("mistral-test-key");
        _ = services.AddMistralAILlmModel(new ModelAlias("fast"), new ModelId("mistral-small-latest"));
        _ = services.AddMistralAILlmModel(new ModelAlias("smart"), new ModelId("mistral-large-latest"));

        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<ILlmModel>().ToArray();

        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["fast", "smart"], ignoreOrder: true);
    }

    [Fact]
    public void AddMistralAILlmModel_WhenResolved_UsesMistralAIProviderIdentity()
    {
        var services = new ServiceCollection();
        _ = services.AddMistralAI();
        _ = services.AddMistralAIApiKeyCredential("mistral-test-key");
        _ = services.AddMistralAILlmModel(new ModelAlias("chat"), new ModelId("mistral-large-latest"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<MistralAILlmModel>();

        model.Alias.ShouldBe(new ModelAlias("chat"));
    }

    [Fact]
    public void AddMistralAIEmbeddingModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddMistralAI();
        _ = services.AddMistralAIApiKeyCredential("mistral-test-key");
        _ = services.AddMistralAIEmbeddingModel(new EmbeddingModelAlias("primary"), new ModelId("mistral-embed"));
        _ = services.AddMistralAIEmbeddingModel(new EmbeddingModelAlias("secondary"), new ModelId("codestral-embed"));

        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<IEmbeddingModel>().ToArray();

        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["primary", "secondary"], ignoreOrder: true);
    }

    [Fact]
    public void AddMistralAIEmbeddingModel_WhenResolved_UsesMistralAIEmbeddingModelType()
    {
        var services = new ServiceCollection();
        _ = services.AddMistralAI();
        _ = services.AddMistralAIApiKeyCredential("mistral-test-key");
        _ = services.AddMistralAIEmbeddingModel(new EmbeddingModelAlias("embed"), new ModelId("mistral-embed"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<IEmbeddingModel>().ShouldBeOfType<MistralAIEmbeddingModel>();

        model.Alias.ShouldBe(new EmbeddingModelAlias("embed"));
    }

    [Fact]
    public void AddMistralAILlmModel_WhenGivenADescriptor_RegistersAnAdapterServingThatExactDescriptor()
    {
        var services = new ServiceCollection();
        _ = services.AddMistralAI();
        _ = services.AddMistralAIApiKeyCredential("mistral-test-key");
        var descriptor = new ModelDescriptor(
            new ModelAlias("exact"),
            MistralAIProviderDefaults.ProviderId,
            MistralAIProviderDefaults.ApiFamily,
            new ModelId("codestral-latest"),
            deploymentId: null,
            MistralAIProviderDefaults.DefaultCapabilities,
            MistralAIProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

        _ = services.AddMistralAILlmModel(descriptor);

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<MistralAILlmModel>();
        model.Alias.ShouldBe(descriptor.Alias);
    }

    [Fact]
    public void AddMistralAILlmModel_WhenDescriptorIsNull_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddMistralAILlmModel(null!)).ParamName.ShouldBe("descriptor");
    }

    [Fact]
    public void AddMistralAILlmModel_WhenDescriptorNamesAnotherProvider_ThrowsArgumentExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();
        var foreign = new ModelDescriptor(
            new ModelAlias("foreign"),
            new ProviderId("someone-else"),
            MistralAIProviderDefaults.ApiFamily,
            new ModelId("codestral-latest"),
            deploymentId: null,
            MistralAIProviderDefaults.DefaultCapabilities,
            MistralAIProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

        var exception = Should.Throw<ArgumentException>(() => services.AddMistralAILlmModel(foreign));

        exception.ParamName.ShouldBe("descriptor");
        services.ShouldBeEmpty();
    }

    [Fact]
    public async Task AddMistralAIKnownLlmModel_WhenModelIsKnown_RegistersAdapterAndIdenticalCatalogDescriptor()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentProviders();
        _ = services.AddMistralAI();
        _ = services.AddMistralAIApiKeyCredential("mistral-test-key");

        _ = services.AddMistralAIKnownLlmModel(new ModelAlias("known"), new ModelId("codestral-latest"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<MistralAILlmModel>();
        var snapshot = await provider.GetRequiredService<IModelCatalog>().GetSnapshotAsync(TestContext.Current.CancellationToken);
        var published = snapshot.ConversationModels.ShouldHaveSingleItem();
        published.Alias.ShouldBe(new ModelAlias("known"));
        published.ProviderId.ShouldBe(MistralAIProviderDefaults.ProviderId);
        published.ApiFamily.ShouldBe(MistralAIProviderDefaults.ApiFamily);
        published.ModelId.ShouldBe(new ModelId("codestral-latest"));
        _ = published.Limits.MaxContextTokens.ShouldNotBeNull();
        model.Alias.ShouldBe(published.Alias);
        provider.GetRequiredService<ILlmModelResolver>().Resolve(published).ShouldBeSameAs(model);
    }

    [Fact]
    public void AddMistralAIKnownLlmModel_WhenModelIsUnknown_ThrowsArgumentExceptionForModelIdBeforeRegistering()
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<ArgumentException>(() => services.AddMistralAIKnownLlmModel(new ModelAlias("x"), new ModelId("no-such-model")));

        exception.ParamName.ShouldBe("modelId");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddMistralAIKnownLlmModel_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddMistralAIKnownLlmModel(new ModelAlias("x"), new ModelId("codestral-latest"))).ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddMistralAIKnownLlmModel_WhenAliasIsDefault_ThrowsArgumentExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddMistralAIKnownLlmModel(default, new ModelId("codestral-latest"))).ParamName.ShouldBe("alias");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddMistralAIKnownLlmModel_WhenModelIdIsDefault_ThrowsArgumentExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddMistralAIKnownLlmModel(new ModelAlias("x"), default)).ParamName.ShouldBe("modelId");
        services.ShouldBeEmpty();
    }

    private sealed class StaticOAuthTokenProviderRegistration: IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new OAuthTokenProviderCredential("token", null));
    }
}
