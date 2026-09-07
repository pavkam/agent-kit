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
    public void AddAzureOpenAI_WhenResourceEndpointNotConfigured_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddAzureOpenAI(_ => { });

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<AzureOpenAIProviderOptions>>().Value);
    }

    [Fact]
    public void AddAzureOpenAI_WhenResourceEndpointIsNotAbsolute_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddAzureOpenAI(options => options.ResourceEndpoint = new Uri("not-absolute", UriKind.Relative));

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<AzureOpenAIProviderOptions>>().Value);
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

    private sealed class StaticOAuthTokenProviderRegistration: IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new OAuthTokenProviderCredential("token", null));
    }
}
