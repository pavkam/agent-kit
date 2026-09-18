// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Verifies the <c>AddGoogleGemini*</c> dependency-injection registration
/// surface: endpoint options, credential source selection, and additive
/// model registration.
/// </summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddGoogleGemini_WhenNoOptionsConfigured_RegistersDefaultOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleGemini();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GoogleGeminiProviderOptions>>().Value;

        options.BaseAddress.ShouldBe(GoogleGeminiProviderDefaults.DefaultBaseAddress);
        options.ApiVersion.ShouldBe(GoogleGeminiProviderDefaults.DefaultApiVersion);
        options.PreferStreaming.ShouldBeTrue();
    }

    [Fact]
    public void AddGoogleGemini_WhenBaseAddressIsNotAbsolute_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleGemini(options => options.BaseAddress = new Uri("not-absolute", UriKind.Relative));

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<GoogleGeminiProviderOptions>>().Value);
    }

    [Fact]
    public void AddGoogleGemini_WhenApiVersionIsWhitespace_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleGemini(options => options.ApiVersion = "   ");

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<GoogleGeminiProviderOptions>>().Value);
    }

    [Fact]
    public void AddGoogleGeminiApiKeyCredential_WhenRegistered_ResolvesApiKeyCredential()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleGemini();
        _ = services.AddGoogleGeminiApiKeyCredential("AIza-test-key");

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(GoogleGeminiProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<StaticApiKeyCredentialSource>();
    }

    [Fact]
    public void AddGoogleGeminiOAuthCredential_WhenRegistered_ResolvesDelegatingOAuthSource()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleGemini();
        _ = services.AddGoogleGeminiOAuthCredential<StaticOAuthTokenProviderRegistration>();

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(GoogleGeminiProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<DelegatingOAuthCredentialSource>();
    }

    [Fact]
    public void AddGoogleGeminiLlmModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleGemini();
        _ = services.AddGoogleGeminiApiKeyCredential("AIza-test-key");
        _ = services.AddGoogleGeminiLlmModel(new ModelAlias("fast"), new ModelId("gemini-2.5-flash"));
        _ = services.AddGoogleGeminiLlmModel(new ModelAlias("smart"), new ModelId("gemini-2.5-pro"));

        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<ILlmModel>().ToArray();

        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["fast", "smart"], ignoreOrder: true);
    }

    [Fact]
    public void AddGoogleGeminiLlmModel_WhenResolved_UsesGoogleGeminiProviderIdentity()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleGemini();
        _ = services.AddGoogleGeminiApiKeyCredential("AIza-test-key");
        _ = services.AddGoogleGeminiLlmModel(new ModelAlias("chat"), new ModelId("gemini-2.5-flash"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<GoogleGeminiLlmModel>();

        model.Alias.ShouldBe(new ModelAlias("chat"));
    }

    [Fact]
    public void AddGoogleGeminiEmbeddingModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleGemini();
        _ = services.AddGoogleGeminiApiKeyCredential("AIza-test-key");
        _ = services.AddGoogleGeminiEmbeddingModel(new EmbeddingModelAlias("small"), new ModelId("text-embedding-004"));
        _ = services.AddGoogleGeminiEmbeddingModel(new EmbeddingModelAlias("large"), new ModelId("gemini-embedding-001"));

        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<IEmbeddingModel>().ToArray();

        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["small", "large"], ignoreOrder: true);
    }

    [Fact]
    public void AddGoogleGeminiEmbeddingModel_WhenResolved_UsesGoogleGeminiEmbeddingModelType()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleGemini();
        _ = services.AddGoogleGeminiApiKeyCredential("AIza-test-key");
        _ = services.AddGoogleGeminiEmbeddingModel(new EmbeddingModelAlias("embed"), new ModelId("text-embedding-004"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<IEmbeddingModel>().ShouldBeOfType<GoogleGeminiEmbeddingModel>();

        model.Alias.ShouldBe(new EmbeddingModelAlias("embed"));
    }

    [Fact]
    public void AddGoogleGeminiLlmModel_WhenGivenADescriptor_RegistersAnAdapterServingThatExactDescriptor()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleGemini();
        _ = services.AddGoogleGeminiApiKeyCredential("AIza-test-key");
        var descriptor = new ModelDescriptor(
            new ModelAlias("exact"),
            GoogleGeminiProviderDefaults.ProviderId,
            GoogleGeminiProviderDefaults.ApiFamily,
            new ModelId("gemini-2.5-flash"),
            deploymentId: null,
            GoogleGeminiProviderDefaults.DefaultCapabilities,
            GoogleGeminiProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

        _ = services.AddGoogleGeminiLlmModel(descriptor);

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<GoogleGeminiLlmModel>();
        model.Alias.ShouldBe(descriptor.Alias);
    }

    [Fact]
    public void AddGoogleGeminiLlmModel_WhenDescriptorIsNull_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddGoogleGeminiLlmModel(null!)).ParamName.ShouldBe("descriptor");
    }

    [Fact]
    public void AddGoogleGeminiLlmModel_WhenDescriptorNamesAnotherProvider_ThrowsArgumentExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();
        var foreign = new ModelDescriptor(
            new ModelAlias("foreign"),
            new ProviderId("someone-else"),
            GoogleGeminiProviderDefaults.ApiFamily,
            new ModelId("gemini-2.5-flash"),
            deploymentId: null,
            GoogleGeminiProviderDefaults.DefaultCapabilities,
            GoogleGeminiProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

        var exception = Should.Throw<ArgumentException>(() => services.AddGoogleGeminiLlmModel(foreign));

        exception.ParamName.ShouldBe("descriptor");
        services.ShouldBeEmpty();
    }

    [Fact]
    public async Task AddGoogleGeminiKnownLlmModel_WhenModelIsKnown_RegistersAdapterAndIdenticalCatalogDescriptor()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentProviders();
        _ = services.AddGoogleGemini();
        _ = services.AddGoogleGeminiApiKeyCredential("AIza-test-key");

        _ = services.AddGoogleGeminiKnownLlmModel(new ModelAlias("known"), new ModelId("gemini-2.5-flash"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<GoogleGeminiLlmModel>();
        var snapshot = await provider.GetRequiredService<IModelCatalog>().GetSnapshotAsync(TestContext.Current.CancellationToken);
        var published = snapshot.ConversationModels.ShouldHaveSingleItem();
        published.Alias.ShouldBe(new ModelAlias("known"));
        published.ProviderId.ShouldBe(GoogleGeminiProviderDefaults.ProviderId);
        published.ApiFamily.ShouldBe(GoogleGeminiProviderDefaults.ApiFamily);
        published.ModelId.ShouldBe(new ModelId("gemini-2.5-flash"));
        _ = published.Limits.MaxContextTokens.ShouldNotBeNull();
        model.Alias.ShouldBe(published.Alias);
        provider.GetRequiredService<ILlmModelResolver>().Resolve(published).ShouldBeSameAs(model);
    }

    [Fact]
    public void AddGoogleGeminiKnownLlmModel_WhenModelIsUnknown_ThrowsArgumentExceptionForModelIdBeforeRegistering()
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<ArgumentException>(() => services.AddGoogleGeminiKnownLlmModel(new ModelAlias("x"), new ModelId("no-such-model")));

        exception.ParamName.ShouldBe("modelId");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddGoogleGeminiKnownLlmModel_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddGoogleGeminiKnownLlmModel(new ModelAlias("x"), new ModelId("gemini-2.5-flash"))).ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddGoogleGeminiKnownLlmModel_WhenAliasIsDefault_ThrowsArgumentExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddGoogleGeminiKnownLlmModel(default, new ModelId("gemini-2.5-flash"))).ParamName.ShouldBe("alias");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddGoogleGeminiKnownLlmModel_WhenModelIdIsDefault_ThrowsArgumentExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddGoogleGeminiKnownLlmModel(new ModelAlias("x"), default)).ParamName.ShouldBe("modelId");
        services.ShouldBeEmpty();
    }

    private sealed class StaticOAuthTokenProviderRegistration: IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new OAuthTokenProviderCredential("token", null));
    }
}
