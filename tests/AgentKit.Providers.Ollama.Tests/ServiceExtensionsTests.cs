// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Ollama.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Verifies the <c>AddOllama*</c> dependency-injection registration surface:
/// endpoint options, credential source selection, and additive model
/// registration.
/// </summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddOllama_WhenNoOptionsConfigured_RegistersDefaultOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddOllama();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<OllamaProviderOptions>>().Value;

        options.BaseAddress.ShouldBe(OllamaProviderDefaults.DefaultBaseAddress);
        options.ChatCompletionsPath.ShouldBe(OllamaProviderDefaults.DefaultChatCompletionsPath);
    }

    [Fact]
    public void AddOllama_WhenBaseAddressIsNotAbsolute_FailsStartupValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddOllama(options => options.BaseAddress = new Uri("not-absolute", UriKind.Relative));

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Theory]
    [InlineData("https://evil.example.test/chat")]
    [InlineData("//evil.example.test/chat")]
    [InlineData("/chat/completions")]
    public void AddOllama_WhenChatPathCanReplaceConfiguredEndpoint_FailsStartupValidation(string path)
    {
        var services = new ServiceCollection();
        _ = services.AddOllama(options => options.ChatCompletionsPath = path);
        using var provider = services.BuildServiceProvider();
        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Theory]
    [InlineData("https://evil.example.test/embeddings")]
    [InlineData("/embeddings")]
    public void AddOllama_WhenEmbeddingsPathCanReplaceConfiguredEndpoint_FailsStartupValidation(string path)
    {
        var services = new ServiceCollection();
        _ = services.AddOllama(options => options.EmbeddingsPath = path);
        using var provider = services.BuildServiceProvider();
        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void AddOllamaApiKeyCredential_WhenRegistered_ResolvesApiKeyCredential()
    {
        var services = new ServiceCollection();
        _ = services.AddOllama();
        _ = services.AddOllamaApiKeyCredential("test-key");

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(
            OllamaProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<StaticApiKeyCredentialSource>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddOllamaApiKeyCredential_WhenAnotherProviderCredentialCoexists_ResolvesByProviderIdentity(
        bool ollamaRegisteredFirst)
    {
        var services = new ServiceCollection();
        var otherProviderId = new ProviderId("other-provider");
        var otherSource = new StaticApiKeyCredentialSource("other-key");

        if (ollamaRegisteredFirst)
        {
            _ = services.AddOllamaApiKeyCredential("ollama-key");
            _ = services.AddKeyedSingleton<IProviderCredentialSource>(otherProviderId, otherSource);
        }
        else
        {
            _ = services.AddKeyedSingleton<IProviderCredentialSource>(otherProviderId, otherSource);
            _ = services.AddOllamaApiKeyCredential("ollama-key");
        }

        await using var provider = services.BuildServiceProvider();
        var ollamaSource = provider.GetRequiredKeyedService<IProviderCredentialSource>(
            OllamaProviderDefaults.ProviderId);
        var resolvedOtherSource = provider.GetRequiredKeyedService<IProviderCredentialSource>(otherProviderId);

        var ollamaCredential = await ollamaSource.GetCredentialAsync(
            OllamaProviderDefaults.ProviderId,
            TestContext.Current.CancellationToken);
        var otherCredential = await resolvedOtherSource.GetCredentialAsync(
            otherProviderId,
            TestContext.Current.CancellationToken);

        ollamaCredential.ShouldBeOfType<ApiKeyProviderCredential>().ApiKey.ShouldBe("ollama-key");
        otherCredential.ShouldBeOfType<ApiKeyProviderCredential>().ApiKey.ShouldBe("other-key");
    }

    [Fact]
    public void AddOllamaOAuthCredential_WhenRegistered_ResolvesDelegatingOAuthSource()
    {
        var services = new ServiceCollection();
        _ = services.AddOllama();
        _ = services.AddOllamaOAuthCredential<StaticOAuthTokenProviderRegistration>();

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(
            OllamaProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<DelegatingOAuthCredentialSource>();
    }

    [Fact]
    public void AddOllamaLlmModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddOllama();
        _ = services.AddOllamaApiKeyCredential("test-key");
        _ = services.AddOllamaLlmModel(new ModelAlias("primary"), new ModelId("llama3.3"));
        _ = services.AddOllamaLlmModel(new ModelAlias("secondary"), new ModelId("qwen2.5"));

        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<ILlmModel>().ToArray();

        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["primary", "secondary"], ignoreOrder: true);
    }

    [Fact]
    public void AddOllamaLlmModel_WhenResolved_UsesOllamaProviderIdentity()
    {
        var services = new ServiceCollection();
        _ = services.AddOllama();
        _ = services.AddOllamaApiKeyCredential("test-key");
        _ = services.AddOllamaLlmModel(new ModelAlias("chat"), new ModelId("llama3.3"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<OllamaLlmModel>();

        model.Alias.ShouldBe(new ModelAlias("chat"));
    }

    [Fact]
    public void AddOllamaEmbeddingModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddOllama();
        _ = services.AddOllamaApiKeyCredential("test-key");
        _ = services.AddOllamaEmbeddingModel(new EmbeddingModelAlias("primary"), new ModelId("nomic-embed-text"));
        _ = services.AddOllamaEmbeddingModel(new EmbeddingModelAlias("secondary"), new ModelId("mxbai-embed-large"));

        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<IEmbeddingModel>().ToArray();

        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["primary", "secondary"], ignoreOrder: true);
    }

    [Fact]
    public void AddOllamaEmbeddingModel_WhenResolved_UsesOllamaEmbeddingModelType()
    {
        var services = new ServiceCollection();
        _ = services.AddOllama();
        _ = services.AddOllamaApiKeyCredential("test-key");
        _ = services.AddOllamaEmbeddingModel(new EmbeddingModelAlias("embed"), new ModelId("nomic-embed-text"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<IEmbeddingModel>().ShouldBeOfType<OllamaEmbeddingModel>();

        model.Alias.ShouldBe(new EmbeddingModelAlias("embed"));
    }

    [Fact]
    public void AddOllamaLlmModel_WhenGivenADescriptor_RegistersAnAdapterServingThatExactDescriptor()
    {
        var services = new ServiceCollection();
        _ = services.AddOllama();
        _ = services.AddOllamaApiKeyCredential("test-key");
        var descriptor = new ModelDescriptor(
            new ModelAlias("exact"),
            OllamaProviderDefaults.ProviderId,
            OllamaProviderDefaults.ApiFamily,
            new ModelId("llama3.3"),
            deploymentId: null,
            OllamaProviderDefaults.DefaultCapabilities,
            OllamaProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

        _ = services.AddOllamaLlmModel(descriptor);

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<OllamaLlmModel>();
        model.Alias.ShouldBe(descriptor.Alias);
    }

    [Fact]
    public void AddOllamaLlmModel_WhenDescriptorIsNull_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddOllamaLlmModel(null!)).ParamName.ShouldBe("descriptor");
    }

    [Fact]
    public void AddOllamaLlmModel_WhenDescriptorNamesAnotherProvider_ThrowsArgumentExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();
        var foreign = new ModelDescriptor(
            new ModelAlias("foreign"),
            new ProviderId("someone-else"),
            OllamaProviderDefaults.ApiFamily,
            new ModelId("llama3.3"),
            deploymentId: null,
            OllamaProviderDefaults.DefaultCapabilities,
            OllamaProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

        var exception = Should.Throw<ArgumentException>(() => services.AddOllamaLlmModel(foreign));

        exception.ParamName.ShouldBe("descriptor");
        services.ShouldBeEmpty();
    }

    private sealed class StaticOAuthTokenProviderRegistration: IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new OAuthTokenProviderCredential("token", null));
    }
}
