// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.DeepSeek.Tests;

using System.Diagnostics;

using AgentKit.Providers.Groq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddDeepSeek_WhenNoOptionsConfigured_RegistersDefaultOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddDeepSeek();
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<DeepSeekProviderOptions>>().Value;
        options.BaseAddress.ShouldBe(DeepSeekProviderDefaults.DefaultBaseAddress);
        options.ChatCompletionsPath.ShouldBe(DeepSeekProviderDefaults.DefaultChatCompletionsPath);
    }

    [Fact]
    public void AddDeepSeek_WhenBaseAddressIsNotAbsolute_FailsStartupValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddDeepSeek(options => options.BaseAddress = new Uri("not-absolute", UriKind.Relative));
        using var provider = services.BuildServiceProvider();
        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Theory]
    [InlineData("https://evil.example.test/chat")]
    [InlineData("//evil.example.test/chat")]
    [InlineData("/chat/completions")]
    public void AddDeepSeek_WhenChatPathCanReplaceConfiguredEndpoint_FailsStartupValidation(string path)
    {
        var services = new ServiceCollection();
        _ = services.AddDeepSeek(options => options.ChatCompletionsPath = path);
        using var provider = services.BuildServiceProvider();
        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void AddDeepSeekApiKeyCredential_WhenRegistered_ResolvesApiKeyCredential()
    {
        var services = new ServiceCollection();
        _ = services.AddDeepSeek();
        _ = services.AddDeepSeekApiKeyCredential("ds-test-key");
        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(DeepSeekProviderDefaults.ProviderId);
        _ = source.ShouldBeOfType<StaticApiKeyCredentialSource>();
    }

    [Fact]
    public void AddDeepSeekOAuthCredential_WhenRegistered_ResolvesDelegatingOAuthSource()
    {
        var services = new ServiceCollection();
        _ = services.AddDeepSeek();
        _ = services.AddDeepSeekOAuthCredential<StaticOAuthTokenProviderRegistration>();
        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(DeepSeekProviderDefaults.ProviderId);
        _ = source.ShouldBeOfType<DelegatingOAuthCredentialSource>();
    }

    [Fact]
    public void AddDeepSeekLlmModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddDeepSeek();
        _ = services.AddDeepSeekApiKeyCredential("ds-test-key");
        _ = services.AddDeepSeekLlmModel(new ModelAlias("chat"), new ModelId("deepseek-chat"));
        _ = services.AddDeepSeekLlmModel(new ModelAlias("reasoner"), new ModelId("deepseek-reasoner"));
        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<ILlmModel>().ToArray();
        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["chat", "reasoner"], ignoreOrder: true);
    }

    [Fact]
    public void AddDeepSeekLlmModel_WhenResolved_UsesDeepSeekProviderIdentity()
    {
        var services = new ServiceCollection();
        _ = services.AddDeepSeek();
        _ = services.AddDeepSeekApiKeyCredential("ds-test-key");
        _ = services.AddDeepSeekLlmModel(new ModelAlias("chat"), new ModelId("deepseek-chat"));
        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<DeepSeekLlmModel>();
        model.Alias.ShouldBe(new ModelAlias("chat"));
    }

    private sealed class StaticOAuthTokenProviderRegistration: IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(new OAuthTokenProviderCredential("token", null));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddProviderCredentials_WhenDeepSeekAndGroqCoexist_ResolvesByProviderIdentityRegardlessOfOrder(bool registerDeepSeekFirst)
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
        var models = provider.GetServices<ILlmModel>().ToArray();
        models.Select(model => model.Alias.Value).ShouldBe(["deepseek-chat", "groq-chat"], ignoreOrder: true);
        var deepSeekCredential = await ResolveApiKeyAsync(provider, DeepSeekProviderDefaults.ProviderId);
        var groqCredential = await ResolveApiKeyAsync(provider, GroqProviderDefaults.ProviderId);
        deepSeekCredential.ApiKey.ShouldBe("deepseek-key");
        groqCredential.ApiKey.ShouldBe("groq-key");
    }

    private static void RegisterDeepSeek(IServiceCollection services)
    {
        Debug.Assert(services is not null, "The caller must provide a service collection.");
        _ = services.AddDeepSeek();
        _ = services.AddDeepSeekApiKeyCredential("deepseek-key");
        _ = services.AddDeepSeekLlmModel(new ModelAlias("deepseek-chat"), new ModelId("deepseek-chat"));
    }

    private static void RegisterGroq(IServiceCollection services)
    {
        Debug.Assert(services is not null, "The caller must provide a service collection.");
        _ = services.AddGroq();
        _ = services.AddGroqApiKeyCredential("groq-key");
        _ = services.AddGroqLlmModel(new ModelAlias("groq-chat"), new ModelId("llama-3.3-70b-versatile"));
    }

    private static async ValueTask<ApiKeyProviderCredential> ResolveApiKeyAsync(IServiceProvider provider, ProviderId providerId)
    {
        Debug.Assert(provider is not null, "The caller must provide a service provider.");
        Debug.Assert(!string.IsNullOrWhiteSpace(providerId.Value), "The caller must provide a valid provider identity.");
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(providerId);
        var credential = await source.GetCredentialAsync(providerId, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return credential.ShouldBeOfType<ApiKeyProviderCredential>();
    }
}
