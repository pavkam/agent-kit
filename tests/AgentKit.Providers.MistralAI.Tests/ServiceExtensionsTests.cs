// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Verifies the <c>AddMistralAI*</c> dependency-injection registration
/// surface: endpoint options, credential source selection, and additive
/// model registration.
/// </summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddMistralAI_WhenNoOptionsConfigured_RegistersDefaultOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddMistralAI();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MistralAIProviderOptions>>().Value;

        options.BaseAddress.ShouldBe(MistralAIProviderDefaults.DefaultBaseAddress);
        options.ChatCompletionsPath.ShouldBe(MistralAIProviderDefaults.DefaultChatCompletionsPath);
        options.PreferStreaming.ShouldBeTrue();
    }

    [Fact]
    public void AddMistralAI_WhenBaseAddressIsNotAbsolute_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddMistralAI(options => options.BaseAddress = new Uri("not-absolute", UriKind.Relative));

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<MistralAIProviderOptions>>().Value);
    }

    [Fact]
    public void AddMistralAI_WhenChatCompletionsPathIsWhitespace_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddMistralAI(options => options.ChatCompletionsPath = "   ");

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<MistralAIProviderOptions>>().Value);
    }

    [Fact]
    public void AddMistralAIApiKeyCredential_WhenRegistered_ResolvesApiKeyCredential()
    {
        var services = new ServiceCollection();
        _ = services.AddMistralAI();
        _ = services.AddMistralAIApiKeyCredential("mistral-test-key");

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(MistralAIProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<StaticApiKeyCredentialSource>();
    }

    [Fact]
    public void AddMistralAIOAuthCredential_WhenRegistered_ResolvesDelegatingOAuthSource()
    {
        var services = new ServiceCollection();
        _ = services.AddMistralAI();
        _ = services.AddMistralAIOAuthCredential<StaticOAuthTokenProviderRegistration>();

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(MistralAIProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<DelegatingOAuthCredentialSource>();
    }

    [Fact]
    public void AddMistralAILlmModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddMistralAI();
        _ = services.AddMistralAIApiKeyCredential("mistral-test-key");
        _ = services.AddMistralAILlmModel(new ModelAlias("fast"), new ModelId("mistral-small-latest"));
        _ = services.AddMistralAILlmModel(new ModelAlias("smart"), new ModelId("mistral-large-latest"));

        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<ILlmModel>().ToArray();

        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["fast", "smart"], ignoreOrder: true);
    }

    [Fact]
    public void AddMistralAILlmModel_WhenResolved_UsesMistralAIProviderIdentity()
    {
        var services = new ServiceCollection();
        _ = services.AddMistralAI();
        _ = services.AddMistralAIApiKeyCredential("mistral-test-key");
        _ = services.AddMistralAILlmModel(new ModelAlias("chat"), new ModelId("mistral-large-latest"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<MistralAILlmModel>();

        model.Alias.ShouldBe(new ModelAlias("chat"));
    }

    private sealed class StaticOAuthTokenProviderRegistration: IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new OAuthTokenProviderCredential("token", null));
    }
}
