// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Groq.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Verifies the <c>AddGroq*</c> dependency-injection registration surface:
/// endpoint options, credential source selection, and additive model
/// registration.
/// </summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddGroq_WhenNoOptionsConfigured_RegistersDefaultOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddGroq();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GroqProviderOptions>>().Value;

        options.BaseAddress.ShouldBe(GroqProviderDefaults.DefaultBaseAddress);
        options.ChatCompletionsPath.ShouldBe(GroqProviderDefaults.DefaultChatCompletionsPath);
    }

    [Fact]
    public void AddGroq_WhenBaseAddressIsNotAbsolute_FailsStartupValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddGroq(options => options.BaseAddress = new Uri("not-absolute", UriKind.Relative));

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Theory]
    [InlineData("https://evil.example.test/chat")]
    [InlineData("//evil.example.test/chat")]
    [InlineData("/chat/completions")]
    public void AddGroq_WhenChatPathCanReplaceConfiguredEndpoint_FailsStartupValidation(string path)
    {
        var services = new ServiceCollection();
        _ = services.AddGroq(options => options.ChatCompletionsPath = path);
        using var provider = services.BuildServiceProvider();
        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void AddGroqApiKeyCredential_WhenRegistered_ResolvesApiKeyCredential()
    {
        var services = new ServiceCollection();
        _ = services.AddGroq();
        _ = services.AddGroqApiKeyCredential("test-key");

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(
            GroqProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<StaticApiKeyCredentialSource>();
    }

    [Fact]
    public void AddGroqOAuthCredential_WhenRegistered_ResolvesDelegatingOAuthSource()
    {
        var services = new ServiceCollection();
        _ = services.AddGroq();
        _ = services.AddGroqOAuthCredential<StaticOAuthTokenProviderRegistration>();

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(
            GroqProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<DelegatingOAuthCredentialSource>();
    }

    [Fact]
    public void AddGroqLlmModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddGroq();
        _ = services.AddGroqApiKeyCredential("test-key");
        _ = services.AddGroqLlmModel(new ModelAlias("primary"), new ModelId("llama-3.3-70b-versatile"));
        _ = services.AddGroqLlmModel(new ModelAlias("secondary"), new ModelId("openai/gpt-oss-120b"));

        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<ILlmModel>().ToArray();

        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["primary", "secondary"], ignoreOrder: true);
    }

    [Fact]
    public void AddGroqLlmModel_WhenResolved_UsesGroqProviderIdentity()
    {
        var services = new ServiceCollection();
        _ = services.AddGroq();
        _ = services.AddGroqApiKeyCredential("test-key");
        _ = services.AddGroqLlmModel(new ModelAlias("chat"), new ModelId("llama-3.3-70b-versatile"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<GroqLlmModel>();

        model.Alias.ShouldBe(new ModelAlias("chat"));
    }

    [Fact]
    public void AddGroqLlmModel_WhenGivenADescriptor_RegistersAnAdapterServingThatExactDescriptor()
    {
        var services = new ServiceCollection();
        _ = services.AddGroq();
        _ = services.AddGroqApiKeyCredential("test-key");
        var descriptor = new ModelDescriptor(
            new ModelAlias("exact"),
            GroqProviderDefaults.ProviderId,
            GroqProviderDefaults.ApiFamily,
            new ModelId("groq/compound"),
            deploymentId: null,
            GroqProviderDefaults.DefaultCapabilities,
            GroqProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

        _ = services.AddGroqLlmModel(descriptor);

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<GroqLlmModel>();
        model.Alias.ShouldBe(descriptor.Alias);
    }

    [Fact]
    public void AddGroqLlmModel_WhenDescriptorIsNull_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddGroqLlmModel(null!)).ParamName.ShouldBe("descriptor");
    }

    [Fact]
    public void AddGroqLlmModel_WhenDescriptorNamesAnotherProvider_ThrowsArgumentExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();
        var foreign = new ModelDescriptor(
            new ModelAlias("foreign"),
            new ProviderId("someone-else"),
            GroqProviderDefaults.ApiFamily,
            new ModelId("groq/compound"),
            deploymentId: null,
            GroqProviderDefaults.DefaultCapabilities,
            GroqProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

        var exception = Should.Throw<ArgumentException>(() => services.AddGroqLlmModel(foreign));

        exception.ParamName.ShouldBe("descriptor");
        services.ShouldBeEmpty();
    }

    [Fact]
    public async Task AddGroqKnownLlmModel_WhenModelIsKnown_RegistersAdapterAndIdenticalCatalogDescriptor()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentProviders();
        _ = services.AddGroq();
        _ = services.AddGroqApiKeyCredential("test-key");

        _ = services.AddGroqKnownLlmModel(new ModelAlias("known"), new ModelId("groq/compound"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<GroqLlmModel>();
        var snapshot = await provider.GetRequiredService<IModelCatalog>().GetSnapshotAsync(TestContext.Current.CancellationToken);
        var published = snapshot.ConversationModels.ShouldHaveSingleItem();
        published.Alias.ShouldBe(new ModelAlias("known"));
        published.ProviderId.ShouldBe(GroqProviderDefaults.ProviderId);
        published.ApiFamily.ShouldBe(GroqProviderDefaults.ApiFamily);
        published.ModelId.ShouldBe(new ModelId("groq/compound"));
        _ = published.Limits.MaxContextTokens.ShouldNotBeNull();
        model.Alias.ShouldBe(published.Alias);
        provider.GetRequiredService<ILlmModelResolver>().Resolve(published).ShouldBeSameAs(model);
    }

    [Fact]
    public void AddGroqKnownLlmModel_WhenModelIsUnknown_ThrowsArgumentExceptionForModelIdBeforeRegistering()
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<ArgumentException>(() => services.AddGroqKnownLlmModel(new ModelAlias("x"), new ModelId("no-such-model")));

        exception.ParamName.ShouldBe("modelId");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddGroqKnownLlmModel_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddGroqKnownLlmModel(new ModelAlias("x"), new ModelId("groq/compound"))).ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddGroqKnownLlmModel_WhenAliasIsDefault_ThrowsArgumentExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddGroqKnownLlmModel(default, new ModelId("groq/compound"))).ParamName.ShouldBe("alias");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddGroqKnownLlmModel_WhenModelIdIsDefault_ThrowsArgumentExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddGroqKnownLlmModel(new ModelAlias("x"), default)).ParamName.ShouldBe("modelId");
        services.ShouldBeEmpty();
    }

    private sealed class StaticOAuthTokenProviderRegistration: IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new OAuthTokenProviderCredential("token", null));
    }
}
