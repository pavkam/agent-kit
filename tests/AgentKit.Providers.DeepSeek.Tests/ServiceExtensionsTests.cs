// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.DeepSeek.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Verifies the <c>AddDeepSeek*</c> dependency-injection registration
/// surface: endpoint options, credential source selection, and additive
/// model registration.
/// </summary>
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

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void AddDeepSeekApiKeyCredential_WhenRegistered_ResolvesApiKeyCredential()
    {
        var services = new ServiceCollection();
        _ = services.AddDeepSeek();
        _ = services.AddDeepSeekApiKeyCredential("ds-test-key");

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(
            DeepSeekProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<StaticApiKeyCredentialSource>();
    }

    [Fact]
    public void AddDeepSeekOAuthCredential_WhenRegistered_ResolvesDelegatingOAuthSource()
    {
        var services = new ServiceCollection();
        _ = services.AddDeepSeek();
        _ = services.AddDeepSeekOAuthCredential<StaticOAuthTokenProviderRegistration>();

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(
            DeepSeekProviderDefaults.ProviderId);

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
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new OAuthTokenProviderCredential("token", null));
    }
}
