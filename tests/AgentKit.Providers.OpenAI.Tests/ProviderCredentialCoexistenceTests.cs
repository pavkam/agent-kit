// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAI.Tests;

using AgentKit.Providers.OpenRouter;
using AgentKit.Providers.ZAI;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Verifies that branded provider registrations retain independent credential
/// sources when several OpenAI-compatible integrations share one container.
/// </summary>
public sealed class ProviderCredentialCoexistenceTests
{
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
        models.Select(model => model.Alias.Value).ShouldBe(
            ["openai-chat", "openrouter-chat", "zai-chat"],
            ignoreOrder: true);

        var openAICredential = await ResolveApiKeyAsync(
            provider,
            OpenAIProviderDefaults.ProviderId);
        var openRouterCredential = await ResolveApiKeyAsync(
            provider,
            OpenRouterProviderDefaults.ProviderId);
        var zAICredential = await ResolveApiKeyAsync(
            provider,
            ZAIProviderDefaults.ProviderId);

        openAICredential.ApiKey.ShouldBe("openai-key");
        openRouterCredential.ApiKey.ShouldBe("openrouter-key");
        zAICredential.ApiKey.ShouldBe("zai-key");
    }

    private static async ValueTask<ApiKeyProviderCredential> ResolveApiKeyAsync(
        IServiceProvider provider,
        ProviderId providerId)
    {
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(providerId);
        var credential = await source
            .GetCredentialAsync(providerId, TestContext.Current.CancellationToken)
            .ConfigureAwait(false);

        return credential.ShouldBeOfType<ApiKeyProviderCredential>();
    }
}
