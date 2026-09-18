// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AzureOpenAI.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Verifies the <c>AddAzureOpenAI*</c> dependency-injection registration
/// surface: endpoint options, credential source selection, and additive
/// deployment registration.
/// </summary>
public sealed class ServiceExtensionsTests
{
    private static readonly Uri ResourceEndpoint = new("https://my-resource.openai.azure.test/");

    [Fact]
    public void AddAzureOpenAI_WhenResourceEndpointConfigured_RegistersOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddAzureOpenAI(options => options.ResourceEndpoint = ResourceEndpoint);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AzureOpenAIProviderOptions>>().Value;

        options.ResourceEndpoint.ShouldBe(ResourceEndpoint);
        options.ChatCompletionsPath.ShouldBe(AzureOpenAIProviderDefaults.DefaultChatCompletionsPath);
        options.PreferStreaming.ShouldBeTrue();
    }

    [Fact]
    public void AddAzureOpenAI_WhenResourceEndpointNotConfigured_FailsStartupValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddAzureOpenAI(_ => { });

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void AddAzureOpenAI_WhenResourceEndpointIsNotAbsolute_FailsStartupValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddAzureOpenAI(options => options.ResourceEndpoint = new Uri("not-absolute", UriKind.Relative));

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Theory]
    [InlineData("https://evil.example.test/chat")]
    [InlineData("//evil.example.test/chat")]
    [InlineData("/openai/v1/chat/completions")]
    public void AddAzureOpenAI_WhenChatPathCanReplaceConfiguredEndpoint_FailsStartupValidation(string path)
    {
        var services = new ServiceCollection();
        _ = services.AddAzureOpenAI(options =>
        {
            options.ResourceEndpoint = ResourceEndpoint;
            options.ChatCompletionsPath = path;
        });

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void AddAzureOpenAI_WhenEmbeddingsPathCanReplaceConfiguredEndpoint_FailsStartupValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddAzureOpenAI(options =>
        {
            options.ResourceEndpoint = ResourceEndpoint;
            options.EmbeddingsPath = "https://evil.example.test/embeddings";
        });

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void AddAzureOpenAI_WhenCalled_RegistersSharedOpenAICompatibleCollaborators()
    {
        var services = new ServiceCollection();
        _ = services.AddAzureOpenAI(options => options.ResourceEndpoint = ResourceEndpoint);

        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<IOpenAIRequestTranslator>().ShouldBeOfType<OpenAIRequestTranslator>();
        _ = provider.GetRequiredService<IOpenAIStreamParser>().ShouldBeOfType<OpenAIChatCompletionResponseParser>();
        _ = provider.GetRequiredService<IOpenAIEmbeddingRequestTranslator>().ShouldBeOfType<OpenAIEmbeddingRequestTranslator>();
        _ = provider.GetRequiredService<IOpenAIEmbeddingResponseParser>().ShouldBeOfType<OpenAIEmbeddingResponseParser>();
    }

    [Fact]
    public void AddAzureOpenAIApiKeyCredential_WhenRegistered_ResolvesApiKeyCredential()
    {
        var services = new ServiceCollection();
        _ = services.AddAzureOpenAI(options => options.ResourceEndpoint = ResourceEndpoint);
        _ = services.AddAzureOpenAIApiKeyCredential("azure-resource-key");

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(AzureOpenAIProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<StaticApiKeyCredentialSource>();
    }

    [Fact]
    public void AddAzureOpenAIOAuthCredential_WhenRegistered_ResolvesDelegatingOAuthSource()
    {
        var services = new ServiceCollection();
        _ = services.AddAzureOpenAI(options => options.ResourceEndpoint = ResourceEndpoint);
        _ = services.AddAzureOpenAIOAuthCredential<StaticOAuthTokenProviderRegistration>();

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(AzureOpenAIProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<DelegatingOAuthCredentialSource>();
    }

    [Fact]
    public void AddAzureOpenAILlmModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddAzureOpenAI(options => options.ResourceEndpoint = ResourceEndpoint);
        _ = services.AddAzureOpenAIApiKeyCredential("azure-resource-key");
        _ = services.AddAzureOpenAILlmModel(new ModelAlias("fast"), new ModelId("gpt-4o-mini"), new DeploymentId("fast-deployment"));
        _ = services.AddAzureOpenAILlmModel(new ModelAlias("smart"), new ModelId("gpt-4o"), new DeploymentId("smart-deployment"));

        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<ILlmModel>().ToArray();

        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["fast", "smart"], ignoreOrder: true);
    }

    [Fact]
    public void AddAzureOpenAILlmModel_WhenResolved_UsesAzureOpenAIProviderIdentity()
    {
        var services = new ServiceCollection();
        _ = services.AddAzureOpenAI(options => options.ResourceEndpoint = ResourceEndpoint);
        _ = services.AddAzureOpenAIApiKeyCredential("azure-resource-key");
        _ = services.AddAzureOpenAILlmModel(new ModelAlias("chat"), new ModelId("gpt-4o"), new DeploymentId("prod-gpt4o"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<AzureOpenAILlmModel>();

        model.Alias.ShouldBe(new ModelAlias("chat"));
    }

    [Fact]
    public void AddAzureOpenAIEmbeddingModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddAzureOpenAI(options => options.ResourceEndpoint = ResourceEndpoint);
        _ = services.AddAzureOpenAIApiKeyCredential("azure-resource-key");
        _ = services.AddAzureOpenAIEmbeddingModel(
            new EmbeddingModelAlias("small"), new ModelId("text-embedding-3-small"), new DeploymentId("small-deployment"));
        _ = services.AddAzureOpenAIEmbeddingModel(
            new EmbeddingModelAlias("large"), new ModelId("text-embedding-3-large"), new DeploymentId("large-deployment"));

        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<IEmbeddingModel>().ToArray();

        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["small", "large"], ignoreOrder: true);
    }

    [Fact]
    public void AddAzureOpenAIEmbeddingModel_WhenResolved_UsesAzureOpenAIEmbeddingModelType()
    {
        var services = new ServiceCollection();
        _ = services.AddAzureOpenAI(options => options.ResourceEndpoint = ResourceEndpoint);
        _ = services.AddAzureOpenAIApiKeyCredential("azure-resource-key");
        _ = services.AddAzureOpenAIEmbeddingModel(
            new EmbeddingModelAlias("embed"), new ModelId("text-embedding-3-small"), new DeploymentId("prod-embed"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<IEmbeddingModel>().ShouldBeOfType<AzureOpenAIEmbeddingModel>();

        model.Alias.ShouldBe(new EmbeddingModelAlias("embed"));
    }

    [Fact]
    public void AddAzureOpenAILlmModel_WhenGivenADescriptor_RegistersAnAdapterServingThatExactDescriptor()
    {
        var services = new ServiceCollection();
        _ = services.AddAzureOpenAI(options => options.ResourceEndpoint = ResourceEndpoint);
        _ = services.AddAzureOpenAIApiKeyCredential("azure-resource-key");
        var descriptor = new ModelDescriptor(
            new ModelAlias("exact"),
            AzureOpenAIProviderDefaults.ProviderId,
            AzureOpenAIProviderDefaults.ApiFamily,
            new ModelId("gpt-4o-mini"),
            deploymentId: new DeploymentId("test-deployment"),
            AzureOpenAIProviderDefaults.DefaultCapabilities,
            AzureOpenAIProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

        _ = services.AddAzureOpenAILlmModel(descriptor);

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<AzureOpenAILlmModel>();
        model.Alias.ShouldBe(descriptor.Alias);
    }

    [Fact]
    public void AddAzureOpenAILlmModel_WhenDescriptorIsNull_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddAzureOpenAILlmModel(null!)).ParamName.ShouldBe("descriptor");
    }

    [Fact]
    public void AddAzureOpenAILlmModel_WhenDescriptorNamesAnotherProvider_ThrowsArgumentExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();
        var foreign = new ModelDescriptor(
            new ModelAlias("foreign"),
            new ProviderId("someone-else"),
            AzureOpenAIProviderDefaults.ApiFamily,
            new ModelId("gpt-4o-mini"),
            deploymentId: new DeploymentId("test-deployment"),
            AzureOpenAIProviderDefaults.DefaultCapabilities,
            AzureOpenAIProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

        var exception = Should.Throw<ArgumentException>(() => services.AddAzureOpenAILlmModel(foreign));

        exception.ParamName.ShouldBe("descriptor");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddAzureOpenAILlmModel_WhenDescriptorHasNoDeployment_ThrowsArgumentExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();
        var descriptor = new ModelDescriptor(
            new ModelAlias("no-deployment"),
            AzureOpenAIProviderDefaults.ProviderId,
            AzureOpenAIProviderDefaults.ApiFamily,
            new ModelId("gpt-4o-mini"),
            deploymentId: null,
            AzureOpenAIProviderDefaults.DefaultCapabilities,
            AzureOpenAIProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

        var exception = Should.Throw<ArgumentException>(() => services.AddAzureOpenAILlmModel(descriptor));

        exception.ParamName.ShouldBe("descriptor");
        services.ShouldBeEmpty();
    }

    private sealed class StaticOAuthTokenProviderRegistration: IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new OAuthTokenProviderCredential("token", null));
    }
}
