// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenRouter.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Verifies the <c>AddOpenRouter*</c> dependency-injection registration
/// surface: endpoint options, credential source selection, and additive
/// model registration.
/// </summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddOpenRouter_WhenNoOptionsConfigured_RegistersDefaultOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenRouter();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<OpenRouterProviderOptions>>().Value;

        options.BaseAddress.ShouldBe(OpenRouterProviderDefaults.DefaultBaseAddress);
        options.ChatCompletionsPath.ShouldBe(OpenRouterProviderDefaults.DefaultChatCompletionsPath);
        options.HttpReferer.ShouldBeNull();
        options.ApplicationTitle.ShouldBeNull();
        options.IncludeRoutingMetadata.ShouldBeFalse();
    }

    [Fact]
    public void AddOpenRouter_WhenOptionsConfigured_AppliesOverrides()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenRouter(options =>
        {
            options.HttpReferer = "https://example.test/";
            options.ApplicationTitle = "My App";
            options.IncludeRoutingMetadata = true;
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<OpenRouterProviderOptions>>().Value;

        options.HttpReferer.ShouldBe("https://example.test/");
        options.ApplicationTitle.ShouldBe("My App");
        options.IncludeRoutingMetadata.ShouldBeTrue();
    }

    [Fact]
    public void AddOpenRouter_WhenBaseAddressIsNotAbsolute_FailsStartupValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenRouter(options => options.BaseAddress = new Uri("not-absolute", UriKind.Relative));

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Theory]
    [InlineData("https://evil.example.test/chat")]
    [InlineData("//evil.example.test/chat")]
    public void AddOpenRouter_WhenChatPathCanReplaceConfiguredEndpoint_FailsStartupValidation(string path)
    {
        var services = new ServiceCollection();
        _ = services.AddOpenRouter(options => options.ChatCompletionsPath = path);

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void AddOpenRouterApiKeyCredential_WhenRegistered_ResolvesApiKeyCredential()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenRouter();
        _ = services.AddOpenRouterApiKeyCredential("sk-or-test-key");

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(OpenRouterProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<StaticApiKeyCredentialSource>();
    }

    [Fact]
    public void AddOpenRouterOAuthCredential_WhenRegistered_ResolvesDelegatingOAuthSource()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenRouter();
        _ = services.AddOpenRouterOAuthCredential<StaticOAuthTokenProviderRegistration>();

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(OpenRouterProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<DelegatingOAuthCredentialSource>();
    }

    [Fact]
    public void AddOpenRouterChatModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenRouter();
        _ = services.AddOpenRouterApiKeyCredential("sk-or-test-key");
        _ = services.AddOpenRouterChatModel(new ModelAlias("fast"), new ModelId("openai/gpt-4o-mini"));
        _ = services.AddOpenRouterChatModel(new ModelAlias("claude"), new ModelId("anthropic/claude-3.5-sonnet"));

        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<IChatModel>().ToArray();

        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["fast", "claude"], ignoreOrder: true);
    }

    [Fact]
    public void AddOpenRouterChatModel_WhenResolved_UsesOpenRouterProviderIdentity()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenRouter();
        _ = services.AddOpenRouterApiKeyCredential("sk-or-test-key");
        _ = services.AddOpenRouterChatModel(new ModelAlias("chat"), new ModelId("openai/gpt-4o"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<IChatModel>().ShouldBeOfType<OpenRouterChatModel>();

        model.Alias.ShouldBe(new ModelAlias("chat"));
    }

    private sealed class StaticOAuthTokenProviderRegistration: IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new OAuthTokenProviderCredential("token", null));
    }
}
