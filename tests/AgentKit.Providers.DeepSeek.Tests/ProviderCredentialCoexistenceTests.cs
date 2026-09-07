// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.DeepSeek.Tests;

using System.Diagnostics;

using AgentKit.Providers.Groq;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Verifies that DeepSeek and Groq retain independent credential sources when
/// both branded integrations share one dependency-injection container.
/// </summary>
public sealed class ProviderCredentialCoexistenceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddProviderCredentials_WhenDeepSeekAndGroqCoexist_ResolvesByProviderIdentityRegardlessOfOrder(
        bool registerDeepSeekFirst)
    {
        var services = new ServiceCollection();

        if (registerDeepSeekFirst)
        {
            RegisterDeepSeek(services);
            RegisterGroq(services);
        }
        else
        {
            RegisterGroq(services);
            RegisterDeepSeek(services);
        }

        await using var provider = services.BuildServiceProvider();

        var models = provider.GetServices<IChatModel>().ToArray();
        models.Select(model => model.Alias.Value).ShouldBe(
            ["deepseek-chat", "groq-chat"],
            ignoreOrder: true);

        var deepSeekCredential = await ResolveApiKeyAsync(
            provider,
            DeepSeekProviderDefaults.ProviderId);
        var groqCredential = await ResolveApiKeyAsync(
            provider,
            GroqProviderDefaults.ProviderId);

        deepSeekCredential.ApiKey.ShouldBe("deepseek-key");
        groqCredential.ApiKey.ShouldBe("groq-key");
    }

    private static void RegisterDeepSeek(IServiceCollection services)
    {
        Debug.Assert(services is not null, "The caller must provide a service collection.");

        _ = services.AddDeepSeek();
        _ = services.AddDeepSeekApiKeyCredential("deepseek-key");
        _ = services.AddDeepSeekChatModel(
            new ModelAlias("deepseek-chat"),
            new ModelId("deepseek-chat"));
    }

    private static void RegisterGroq(IServiceCollection services)
    {
        Debug.Assert(services is not null, "The caller must provide a service collection.");

        _ = services.AddGroq();
        _ = services.AddGroqApiKeyCredential("groq-key");
        _ = services.AddGroqChatModel(
            new ModelAlias("groq-chat"),
            new ModelId("llama-3.3-70b-versatile"));
    }

    private static async ValueTask<ApiKeyProviderCredential> ResolveApiKeyAsync(
        IServiceProvider provider,
        ProviderId providerId)
    {
        Debug.Assert(provider is not null, "The caller must provide a service provider.");
        Debug.Assert(
            !string.IsNullOrWhiteSpace(providerId.Value),
            "The caller must provide a valid provider identity.");

        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(providerId);
        var credential = await source
            .GetCredentialAsync(providerId, TestContext.Current.CancellationToken)
            .ConfigureAwait(false);

        return credential.ShouldBeOfType<ApiKeyProviderCredential>();
    }
}
