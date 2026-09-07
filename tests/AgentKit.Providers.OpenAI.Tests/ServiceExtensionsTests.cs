// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAI.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Verifies the <c>AddOpenAI*</c> dependency-injection registration
/// surface: endpoint options, credential source selection, and additive
/// model registration.
/// </summary>
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
    public void AddOpenAI_WhenBaseAddressIsNotAbsolute_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenAI(options => options.BaseAddress = new Uri("not-absolute", UriKind.Relative));

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<OpenAIProviderOptions>>().Value);
    }

    [Fact]
    public void AddOpenAIApiKeyCredential_WhenRegistered_ResolvesApiKeyCredential()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenAI();
        _ = services.AddOpenAIApiKeyCredential("sk-test-key");

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredService<IProviderCredentialSource>();

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
        var source = provider.GetRequiredService<IProviderCredentialSource>();

        _ = source.ShouldBeOfType<DelegatingOAuthCredentialSource>();
    }

    [Fact]
    public async Task AddOpenAIChatModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenAI();
        _ = services.AddOpenAIApiKeyCredential("sk-test-key");
        _ = services.AddOpenAIChatModel(new ModelAlias("fast"), new ModelId("gpt-4o-mini"));
        _ = services.AddOpenAIChatModel(new ModelAlias("smart"), new ModelId("gpt-4o"));

        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<IChatModel>().ToArray();

        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["fast", "smart"], ignoreOrder: true);

        await Task.CompletedTask;
    }

    [Fact]
    public void AddOpenAIChatModel_WhenNoCapabilitiesSupplied_UsesDefaultCapabilities()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenAI();
        _ = services.AddOpenAIApiKeyCredential("sk-test-key");
        _ = services.AddOpenAIChatModel(new ModelAlias("chat"), new ModelId("gpt-4o"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<IChatModel>().ShouldBeOfType<OpenAIChatModel>();

        model.Alias.ShouldBe(new ModelAlias("chat"));
    }

    private sealed class StaticOAuthTokenProviderRegistration: IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new OAuthTokenProviderCredential("token", null));
    }
}
