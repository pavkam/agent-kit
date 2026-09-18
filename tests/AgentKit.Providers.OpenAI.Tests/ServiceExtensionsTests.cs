// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAI.Tests;

using System.Net;

using AgentKit.Providers;
using AgentKit.Providers.OpenAI.Tests.Fakes;
using AgentKit.Providers.OpenRouter;
using AgentKit.Providers.ZAI;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddOpenAI_WhenNoOptionsConfigured_RegistersDefaultOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenAI();
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<OpenAIProviderOptions>>().Value;
        options.BaseAddress.ShouldBe(OpenAIProviderDefaults.DefaultBaseAddress);
        options.ChatCompletionsPath.ShouldBe(OpenAIProviderDefaults.DefaultChatCompletionsPath);
    }

    [Fact]
    public void AddOpenAI_WhenRegistered_DisablesTheHttpClientTimeoutInFavorOfThePerRequestDeadline()
    {
        // The BCL default HttpClient.Timeout (100s) would otherwise bound every buffered attempt
        // regardless of the caller's LlmModelRequest.Deadline, since this adapter's own deadlineSource
        // is layered on top of, not instead of, the transport-level timeout.
        var services = new ServiceCollection();
        _ = services.AddOpenAI();
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<HttpClient>();

        client.Timeout.ShouldBe(Timeout.InfiniteTimeSpan);
    }

    [Fact]
    public void AddOpenAI_WhenRegistered_BoundsThePooledConnectionLifetimeInsteadOfTheInfiniteDefault()
    {
        // SocketsHttpHandler's default PooledConnectionLifetime is infinite. A process-lifetime HttpClient
        // singleton built over that default never observes a DNS change (e.g. a failed-over endpoint) until the
        // process restarts - the well-documented singleton-HttpClient pitfall.
        var services = new ServiceCollection();
        _ = services.AddOpenAI();
        using var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<SocketsHttpHandler>();

        handler.PooledConnectionLifetime.ShouldBe(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void AddOpenAI_WhenOptionsConfigured_AppliesOverrides()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenAI(options =>
        {
            options.BaseAddress = new Uri("https://example.test/");
            options.PreferStreaming = false;
        });
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<OpenAIProviderOptions>>().Value;
        options.BaseAddress.ShouldBe(new Uri("https://example.test/"));
        options.PreferStreaming.ShouldBeFalse();
    }

    [Fact]
    public void AddOpenAI_WhenBaseAddressIsNotAbsolute_FailsStartupValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenAI(options => options.BaseAddress = new Uri("not-absolute", UriKind.Relative));
        using var provider = services.BuildServiceProvider();
        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Theory]
    [InlineData("https://evil.example.test/chat")]
    [InlineData("//evil.example.test/chat")]
    public void AddOpenAI_WhenChatPathCanReplaceConfiguredEndpoint_FailsStartupValidation(string path)
    {
        var services = new ServiceCollection();
        _ = services.AddOpenAI(options => options.ChatCompletionsPath = path);
        using var provider = services.BuildServiceProvider();
        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void AddOpenAIApiKeyCredential_WhenRegistered_ResolvesApiKeyCredential()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenAI();
        _ = services.AddOpenAIApiKeyCredential("sk-test-key");
        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(OpenAIProviderDefaults.ProviderId);
        _ = source.ShouldBeOfType<StaticApiKeyCredentialSource>();
    }

    [Fact]
    public void AddOpenAIApiKeyCredential_WhenCalledAfterOAuthCredential_DoesNotReplaceIt()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenAI();
        _ = services.AddOpenAIOAuthCredential<StaticOAuthTokenProviderRegistration>();
        _ = services.AddOpenAIApiKeyCredential("sk-test-key");
        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(OpenAIProviderDefaults.ProviderId);
        _ = source.ShouldBeOfType<DelegatingOAuthCredentialSource>();
    }

    [Fact]
    public async Task AddOpenAILlmModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenAI();
        _ = services.AddOpenAIApiKeyCredential("sk-test-key");
        _ = services.AddOpenAILlmModel(new ModelAlias("fast"), new ModelId("gpt-4o-mini"));
        _ = services.AddOpenAILlmModel(new ModelAlias("smart"), new ModelId("gpt-4o"));
        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<ILlmModel>().ToArray();
        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["fast", "smart"], ignoreOrder: true);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ProviderCredentials_WhenThreeProvidersCoexist_ResolveByProviderIdentity()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenAIApiKeyCredential("openai-key");
        _ = services.AddOpenRouterApiKeyCredential("openrouter-key");
        _ = services.AddZAIApiKeyCredential("zai-key");
        using var provider = services.BuildServiceProvider();
        var openAI = provider.GetRequiredKeyedService<IProviderCredentialSource>(OpenAIProviderDefaults.ProviderId);
        var openRouter = provider.GetRequiredKeyedService<IProviderCredentialSource>(OpenRouterProviderDefaults.ProviderId);
        var zAI = provider.GetRequiredKeyedService<IProviderCredentialSource>(ZAIProviderDefaults.ProviderId);
        var openAICredential = await openAI.GetCredentialAsync(OpenAIProviderDefaults.ProviderId, TestContext.Current.CancellationToken);
        var openRouterCredential = await openRouter.GetCredentialAsync(OpenRouterProviderDefaults.ProviderId, TestContext.Current.CancellationToken);
        var zAICredential = await zAI.GetCredentialAsync(ZAIProviderDefaults.ProviderId, TestContext.Current.CancellationToken);
        openAICredential.ShouldBeOfType<ApiKeyProviderCredential>().ApiKey.ShouldBe("openai-key");
        openRouterCredential.ShouldBeOfType<ApiKeyProviderCredential>().ApiKey.ShouldBe("openrouter-key");
        zAICredential.ShouldBeOfType<ApiKeyProviderCredential>().ApiKey.ShouldBe("zai-key");
    }

    [Fact]
    public async Task AddOpenAIKnownLlmModel_WhenModelIsKnown_RegistersAdapterAndIdenticalCatalogDescriptor()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/success.json");
        var services = new ServiceCollection();
        _ = services.AddAgentProviders();
        _ = services.AddOpenAI(options => options.PreferStreaming = false);
        _ = services.AddOpenAIApiKeyCredential("sk-test-key");
        _ = services.AddOpenAIKnownLlmModel(new ModelAlias("fast"), new ModelId("gpt-4o-mini"));
        _ = services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));
        using var provider = services.BuildServiceProvider();

        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<OpenAILlmModel>();
        var snapshot = await provider.GetRequiredService<IModelCatalog>().GetSnapshotAsync(TestContext.Current.CancellationToken);

        var published = snapshot.ConversationModels.ShouldHaveSingleItem();
        published.Alias.ShouldBe(new ModelAlias("fast"));
        published.ModelId.ShouldBe(new ModelId("gpt-4o-mini"));
        published.Limits.MaxContextTokens.ShouldBe(128000);
        published.Pricing.ShouldNotBeNull().CostCurrency.ShouldBe("USD");

        // The adapter rejects a request whose selected descriptor differs from its own, so a request built
        // from the published descriptor proves both registrations carry the same value.
        var user = new UserMessage(new MessageId(Guid.NewGuid()), new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()), null, new BranchId(Guid.NewGuid()), null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, [new TextPart("Hi", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var request = new LlmModelRequest(
            new LlmRequestContext(new ModelRequestId(Guid.NewGuid()), published, [user], [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty),
            attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);
        var result = await model.ExecuteAsync(request, new RecordingModelResponseObserver(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ModelAttemptCompleted>();
    }

    [Fact]
    public void AddOpenAIKnownLlmModel_WhenModelIsUnknown_ThrowsArgumentExceptionForModelId()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenAI();

        var exception = Should.Throw<ArgumentException>(() => services.AddOpenAIKnownLlmModel(new ModelAlias("x"), new ModelId("no-such-model")));

        exception.ParamName.ShouldBe("modelId");
    }

    [Fact]
    public void AddOpenAIKnownLlmModel_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddOpenAIKnownLlmModel(new ModelAlias("x"), new ModelId("gpt-4o-mini"))).ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddOpenAIKnownLlmModel_WhenAliasIsDefault_ThrowsArgumentNullExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddOpenAIKnownLlmModel(default, new ModelId("gpt-4o-mini"))).ParamName.ShouldBe("alias");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddOpenAIKnownLlmModel_WhenModelIdIsDefault_ThrowsArgumentNullExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddOpenAIKnownLlmModel(new ModelAlias("x"), default)).ParamName.ShouldBe("modelId");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddOpenAIKnownLlmModel_WhenModelIsUnknown_RegistersNothing()
    {
        var services = new ServiceCollection();

        _ = Should.Throw<ArgumentException>(() => services.AddOpenAIKnownLlmModel(new ModelAlias("x"), new ModelId("no-such-model")));

        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddOpenAIKnownLlmModel_WhenModelBelongsToAnotherProviderInTheCatalog_ThrowsArgumentException()
    {
        // The catalog knows claude-sonnet-4-5 for anthropic; the OpenAI registration must not borrow it.
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddOpenAIKnownLlmModel(new ModelAlias("x"), new ModelId("claude-sonnet-4-5"))).ParamName.ShouldBe("modelId");
    }

    [Fact]
    public async Task AddOpenAIKnownLlmModel_WhenCustomCatalogIsSupplied_UsesItInsteadOfDefault()
    {
        var custom = new KnownModelCatalog(
            KnownModelCatalog.Default.Provenance,
            [
                new KnownModel(OpenAIProviderDefaults.ProviderId, new ModelId("gpt-private"), "Private", KnownModelStatus.Available,
                    supportsReasoning: true, supportsVisionInput: false, supportsToolCalls: true, new ModelLimits(1000, 100), null, null),
            ]);
        var services = new ServiceCollection();
        _ = services.AddAgentProviders();
        _ = services.AddOpenAI();
        _ = services.AddOpenAIApiKeyCredential("sk-test-key");
        _ = services.AddOpenAIKnownLlmModel(new ModelAlias("private"), new ModelId("gpt-private"), custom);
        using var provider = services.BuildServiceProvider();

        var snapshot = await provider.GetRequiredService<IModelCatalog>().GetSnapshotAsync(TestContext.Current.CancellationToken);

        var published = snapshot.ConversationModels.ShouldHaveSingleItem();
        published.ModelId.ShouldBe(new ModelId("gpt-private"));
        published.Capabilities.SupportsReasoning.ShouldBeTrue();
        published.Capabilities.SupportsStreaming.ShouldBe(OpenAIProviderDefaults.DefaultCapabilities.SupportsStreaming);
        published.Limits.ShouldBe(new ModelLimits(1000, 100));
        published.Pricing.ShouldBeNull();
    }

    [Fact]
    public async Task AddOpenAIKnownLlmModel_WhenCalledForTwoModels_RegistersBothAdaptersAndBothDescriptors()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentProviders();
        _ = services.AddOpenAI();
        _ = services.AddOpenAIApiKeyCredential("sk-test-key");
        _ = services.AddOpenAIKnownLlmModel(new ModelAlias("fast"), new ModelId("gpt-4o-mini"));
        _ = services.AddOpenAIKnownLlmModel(new ModelAlias("smart"), new ModelId("gpt-5"));
        using var provider = services.BuildServiceProvider();

        var adapters = provider.GetServices<ILlmModel>().Select(static m => m.Alias.Value).ToArray();
        var snapshot = await provider.GetRequiredService<IModelCatalog>().GetSnapshotAsync(TestContext.Current.CancellationToken);

        adapters.ShouldBe(["fast", "smart"], ignoreOrder: true);
        snapshot.ConversationModels.Select(static m => m.Alias.Value).ShouldBe(["fast", "smart"], ignoreOrder: true);
        snapshot.ConversationModels.Single(static m => m.Alias.Value == "smart").Capabilities.SupportsReasoning.ShouldBeTrue();
        snapshot.ConversationModels.Single(static m => m.Alias.Value == "fast").Capabilities.SupportsReasoning.ShouldBeFalse();
    }

    [Fact]
    public void AddOpenAILlmModel_WhenDescriptorIsNull_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddOpenAILlmModel(descriptor: null!)).ParamName.ShouldBe("descriptor");
    }

    [Fact]
    public void AddOpenAILlmModel_WhenDescriptorNamesAnotherApiFamily_ThrowsArgumentException()
    {
        var services = new ServiceCollection();
        var foreign = new ModelDescriptor(
            new ModelAlias("x"), OpenAIProviderDefaults.ProviderId, new ApiFamilyId("openai-responses"), new ModelId("m"), null,
            OpenAIProviderDefaults.DefaultCapabilities, OpenAIProviderDefaults.DefaultLimits, null, ExtensionData.Empty);

        Should.Throw<ArgumentException>(() => services.AddOpenAILlmModel(foreign)).ParamName.ShouldBe("descriptor");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddOpenAILlmModel_WhenDescriptorIsValid_AdapterExposesItsAlias()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenAI();
        _ = services.AddOpenAIApiKeyCredential("sk-test-key");
        var descriptor = new ModelDescriptor(
            new ModelAlias("exact"), OpenAIProviderDefaults.ProviderId, OpenAIProviderDefaults.ApiFamily, new ModelId("gpt-4o"), null,
            OpenAIProviderDefaults.DefaultCapabilities, new ModelLimits(1, 1), new ModelPricing(1m, 2m, "USD"), ExtensionData.Empty);

        _ = services.AddOpenAILlmModel(descriptor);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ILlmModel>().ShouldBeOfType<OpenAILlmModel>().Alias.ShouldBe(new ModelAlias("exact"));
    }

    [Fact]
    public void AddOpenAILlmModel_WhenDescriptorNamesAnotherProvider_ThrowsArgumentException()
    {
        var services = new ServiceCollection();
        var foreign = new ModelDescriptor(
            new ModelAlias("x"), new ProviderId("anthropic"), OpenAIProviderDefaults.ApiFamily, new ModelId("m"), null,
            OpenAIProviderDefaults.DefaultCapabilities, OpenAIProviderDefaults.DefaultLimits, null, ExtensionData.Empty);

        Should.Throw<ArgumentException>(() => services.AddOpenAILlmModel(foreign)).ParamName.ShouldBe("descriptor");
    }

    [Fact]
    public void AddOpenAILlmModel_WhenNoCapabilitiesSupplied_UsesDefaultCapabilities()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenAI();
        _ = services.AddOpenAIApiKeyCredential("sk-test-key");
        _ = services.AddOpenAILlmModel(new ModelAlias("chat"), new ModelId("gpt-4o"));
        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<OpenAILlmModel>();
        model.Alias.ShouldBe(new ModelAlias("chat"));
    }

    [Fact]
    public void AddOpenAI_WhenEmbeddingsPathCanReplaceConfiguredEndpoint_FailsStartupValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenAI(options => options.EmbeddingsPath = "https://evil.example.test/embeddings");
        using var provider = services.BuildServiceProvider();
        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void AddOpenAIEmbeddingModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenAI();
        _ = services.AddOpenAIApiKeyCredential("sk-test-key");
        _ = services.AddOpenAIEmbeddingModel(new EmbeddingModelAlias("small"), new ModelId("text-embedding-3-small"));
        _ = services.AddOpenAIEmbeddingModel(new EmbeddingModelAlias("large"), new ModelId("text-embedding-3-large"));
        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<IEmbeddingModel>().ToArray();
        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["small", "large"], ignoreOrder: true);
    }

    [Fact]
    public void AddOpenAIEmbeddingModel_WhenResolved_UsesOpenAIEmbeddingModelType()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenAI();
        _ = services.AddOpenAIApiKeyCredential("sk-test-key");
        _ = services.AddOpenAIEmbeddingModel(new EmbeddingModelAlias("embed"), new ModelId("text-embedding-3-small"));
        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<IEmbeddingModel>().ShouldBeOfType<OpenAIEmbeddingModel>();
        model.Alias.ShouldBe(new EmbeddingModelAlias("embed"));
    }

    [Fact]
    public void AddOpenAIEmbeddingModel_AndAddOpenAILlmModel_CoexistIndependently()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenAI();
        _ = services.AddOpenAIApiKeyCredential("sk-test-key");
        _ = services.AddOpenAILlmModel(new ModelAlias("chat"), new ModelId("gpt-4o"));
        _ = services.AddOpenAIEmbeddingModel(new EmbeddingModelAlias("chat"), new ModelId("text-embedding-3-small"));
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ILlmModel>().Alias.ShouldBe(new ModelAlias("chat"));
        provider.GetRequiredService<IEmbeddingModel>().Alias.ShouldBe(new EmbeddingModelAlias("chat"));
    }

    private sealed class StaticOAuthTokenProviderRegistration: IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(new OAuthTokenProviderCredential("token", null));
    }

    [Fact]
    public async Task AddProviderCredentials_WhenAllProvidersCoexist_ResolvesEachProviderOwnCredential()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenAI();
        _ = services.AddOpenAIApiKeyCredential("openai-key");
        _ = services.AddOpenAILlmModel(new ModelAlias("openai-chat"), new ModelId("gpt-test"));
        _ = services.AddOpenRouter();
        _ = services.AddOpenRouterApiKeyCredential("openrouter-key");
        _ = services.AddOpenRouterLlmModel(new ModelAlias("openrouter-chat"), new ModelId("provider/model-test"));
        _ = services.AddZAI();
        _ = services.AddZAIApiKeyCredential("zai-key");
        _ = services.AddZAILlmModel(new ModelAlias("zai-chat"), new ModelId("glm-test"));
        await using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<ILlmModel>().ToArray();
        models.Select(model => model.Alias.Value).ShouldBe(["openai-chat", "openrouter-chat", "zai-chat"], ignoreOrder: true);
        var openAICredential = await ResolveApiKeyAsync(provider, OpenAIProviderDefaults.ProviderId);
        var openRouterCredential = await ResolveApiKeyAsync(provider, OpenRouterProviderDefaults.ProviderId);
        var zAICredential = await ResolveApiKeyAsync(provider, ZAIProviderDefaults.ProviderId);
        openAICredential.ApiKey.ShouldBe("openai-key");
        openRouterCredential.ApiKey.ShouldBe("openrouter-key");
        zAICredential.ApiKey.ShouldBe("zai-key");
    }

    private static async ValueTask<ApiKeyProviderCredential> ResolveApiKeyAsync(IServiceProvider provider, ProviderId providerId)
    {
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(providerId);
        var credential = await source.GetCredentialAsync(providerId, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return credential.ShouldBeOfType<ApiKeyProviderCredential>();
    }
}
