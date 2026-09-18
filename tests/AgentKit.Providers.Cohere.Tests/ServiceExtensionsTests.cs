// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Verifies the <c>AddCohere*</c> dependency-injection registration
/// surface: endpoint options, credential source selection, and additive
/// model registration.
/// </summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddCohere_WhenNoOptionsConfigured_RegistersDefaultOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddCohere();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<CohereProviderOptions>>().Value;

        options.BaseAddress.ShouldBe(CohereProviderDefaults.DefaultBaseAddress);
        options.ChatPath.ShouldBe(CohereProviderDefaults.DefaultChatPath);
        options.PreferStreaming.ShouldBeTrue();
        options.ClientName.ShouldBeNull();
    }

    [Fact]
    public void AddCohere_WhenBaseAddressIsNotAbsolute_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddCohere(options => options.BaseAddress = new Uri("not-absolute", UriKind.Relative));

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<CohereProviderOptions>>().Value);
    }

    [Fact]
    public void AddCohere_WhenChatPathIsWhitespace_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddCohere(options => options.ChatPath = "   ");

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<CohereProviderOptions>>().Value);
    }

    [Fact]
    public void AddCohereApiKeyCredential_WhenRegistered_ResolvesApiKeyCredential()
    {
        var services = new ServiceCollection();
        _ = services.AddCohere();
        _ = services.AddCohereApiKeyCredential("cohere-test-key");

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(CohereProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<StaticApiKeyCredentialSource>();
    }

    [Fact]
    public void AddCohereOAuthCredential_WhenRegistered_ResolvesDelegatingOAuthSource()
    {
        var services = new ServiceCollection();
        _ = services.AddCohere();
        _ = services.AddCohereOAuthCredential<StaticOAuthTokenProviderRegistration>();

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(CohereProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<DelegatingOAuthCredentialSource>();
    }

    [Fact]
    public void AddCohereLlmModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddCohere();
        _ = services.AddCohereApiKeyCredential("cohere-test-key");
        _ = services.AddCohereLlmModel(new ModelAlias("fast"), new ModelId("command-a-05-2026"));
        _ = services.AddCohereLlmModel(new ModelAlias("smart"), new ModelId("command-a-plus-05-2026"));

        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<ILlmModel>().ToArray();

        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["fast", "smart"], ignoreOrder: true);
    }

    [Fact]
    public void AddCohereLlmModel_WhenResolved_UsesCohereProviderIdentity()
    {
        var services = new ServiceCollection();
        _ = services.AddCohere();
        _ = services.AddCohereApiKeyCredential("cohere-test-key");
        _ = services.AddCohereLlmModel(new ModelAlias("chat"), new ModelId("command-a-plus-05-2026"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<CohereLlmModel>();

        model.Alias.ShouldBe(new ModelAlias("chat"));
    }

    [Fact]
    public void AddCohereEmbeddingModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddCohere();
        _ = services.AddCohereApiKeyCredential("cohere-test-key");
        _ = services.AddCohereEmbeddingModel(new EmbeddingModelAlias("primary"), new ModelId("embed-v4.0"));
        _ = services.AddCohereEmbeddingModel(new EmbeddingModelAlias("secondary"), new ModelId("embed-english-v3.0"));

        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<IEmbeddingModel>().ToArray();

        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["primary", "secondary"], ignoreOrder: true);
    }

    [Fact]
    public void AddCohereEmbeddingModel_WhenResolved_UsesCohereEmbeddingModelType()
    {
        var services = new ServiceCollection();
        _ = services.AddCohere();
        _ = services.AddCohereApiKeyCredential("cohere-test-key");
        _ = services.AddCohereEmbeddingModel(new EmbeddingModelAlias("embed"), new ModelId("embed-v4.0"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<IEmbeddingModel>().ShouldBeOfType<CohereEmbeddingModel>();

        model.Alias.ShouldBe(new EmbeddingModelAlias("embed"));
    }

    [Fact]
    public void AddCohereLlmModel_WhenGivenADescriptor_RegistersAnAdapterServingThatExactDescriptor()
    {
        var services = new ServiceCollection();
        _ = services.AddCohere();
        _ = services.AddCohereApiKeyCredential("cohere-test-key");
        var descriptor = new ModelDescriptor(
            new ModelAlias("exact"),
            CohereProviderDefaults.ProviderId,
            CohereProviderDefaults.ApiFamily,
            new ModelId("command-a-03-2025"),
            deploymentId: null,
            CohereProviderDefaults.DefaultCapabilities,
            CohereProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

        _ = services.AddCohereLlmModel(descriptor);

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<CohereLlmModel>();
        model.Alias.ShouldBe(descriptor.Alias);
    }

    [Fact]
    public void AddCohereLlmModel_WhenDescriptorIsNull_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddCohereLlmModel(null!)).ParamName.ShouldBe("descriptor");
    }

    [Fact]
    public void AddCohereLlmModel_WhenDescriptorNamesAnotherProvider_ThrowsArgumentExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();
        var foreign = new ModelDescriptor(
            new ModelAlias("foreign"),
            new ProviderId("someone-else"),
            CohereProviderDefaults.ApiFamily,
            new ModelId("command-a-03-2025"),
            deploymentId: null,
            CohereProviderDefaults.DefaultCapabilities,
            CohereProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

        var exception = Should.Throw<ArgumentException>(() => services.AddCohereLlmModel(foreign));

        exception.ParamName.ShouldBe("descriptor");
        services.ShouldBeEmpty();
    }

    [Fact]
    public async Task AddCohereKnownLlmModel_WhenModelIsKnown_RegistersAdapterAndIdenticalCatalogDescriptor()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentProviders();
        _ = services.AddCohere();
        _ = services.AddCohereApiKeyCredential("cohere-test-key");

        _ = services.AddCohereKnownLlmModel(new ModelAlias("known"), new ModelId("command-a-03-2025"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<CohereLlmModel>();
        var snapshot = await provider.GetRequiredService<IModelCatalog>().GetSnapshotAsync(TestContext.Current.CancellationToken);
        var published = snapshot.ConversationModels.ShouldHaveSingleItem();
        published.Alias.ShouldBe(new ModelAlias("known"));
        published.ProviderId.ShouldBe(CohereProviderDefaults.ProviderId);
        published.ApiFamily.ShouldBe(CohereProviderDefaults.ApiFamily);
        published.ModelId.ShouldBe(new ModelId("command-a-03-2025"));
        _ = published.Limits.MaxContextTokens.ShouldNotBeNull();
        model.Alias.ShouldBe(published.Alias);
        provider.GetRequiredService<ILlmModelResolver>().Resolve(published).ShouldBeSameAs(model);
    }

    [Fact]
    public void AddCohereKnownLlmModel_WhenModelIsUnknown_ThrowsArgumentExceptionForModelIdBeforeRegistering()
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<ArgumentException>(() => services.AddCohereKnownLlmModel(new ModelAlias("x"), new ModelId("no-such-model")));

        exception.ParamName.ShouldBe("modelId");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddCohereKnownLlmModel_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddCohereKnownLlmModel(new ModelAlias("x"), new ModelId("command-a-03-2025"))).ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddCohereKnownLlmModel_WhenAliasIsDefault_ThrowsArgumentExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddCohereKnownLlmModel(default, new ModelId("command-a-03-2025"))).ParamName.ShouldBe("alias");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddCohereKnownLlmModel_WhenModelIdIsDefault_ThrowsArgumentExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddCohereKnownLlmModel(new ModelAlias("x"), default)).ParamName.ShouldBe("modelId");
        services.ShouldBeEmpty();
    }

    private sealed class StaticOAuthTokenProviderRegistration: IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new OAuthTokenProviderCredential("token", null));
    }
}
