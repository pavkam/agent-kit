// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Groq.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Verifies the <c>AddGroq*</c> dependency-injection registration surface:
/// endpoint options, credential source selection, and additive model
/// registration.
/// </summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddGroq_WhenNoOptionsConfigured_RegistersDefaultOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddGroq();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GroqProviderOptions>>().Value;

        options.BaseAddress.ShouldBe(GroqProviderDefaults.DefaultBaseAddress);
        options.ChatCompletionsPath.ShouldBe(GroqProviderDefaults.DefaultChatCompletionsPath);
    }

    [Fact]
    public void AddGroq_WhenBaseAddressIsNotAbsolute_FailsStartupValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddGroq(options => options.BaseAddress = new Uri("not-absolute", UriKind.Relative));

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void AddGroqApiKeyCredential_WhenRegistered_ResolvesApiKeyCredential()
    {
        var services = new ServiceCollection();
        _ = services.AddGroq();
        _ = services.AddGroqApiKeyCredential("test-key");

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(
            GroqProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<StaticApiKeyCredentialSource>();
    }

    [Fact]
    public void AddGroqOAuthCredential_WhenRegistered_ResolvesDelegatingOAuthSource()
    {
        var services = new ServiceCollection();
        _ = services.AddGroq();
        _ = services.AddGroqOAuthCredential<StaticOAuthTokenProviderRegistration>();

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(
            GroqProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<DelegatingOAuthCredentialSource>();
    }

    [Fact]
    public void AddGroqChatModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddGroq();
        _ = services.AddGroqApiKeyCredential("test-key");
        _ = services.AddGroqChatModel(new ModelAlias("primary"), new ModelId("llama-3.3-70b-versatile"));
        _ = services.AddGroqChatModel(new ModelAlias("secondary"), new ModelId("openai/gpt-oss-120b"));

        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<IChatModel>().ToArray();

        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["primary", "secondary"], ignoreOrder: true);
    }

    [Fact]
    public void AddGroqChatModel_WhenResolved_UsesGroqProviderIdentity()
    {
        var services = new ServiceCollection();
        _ = services.AddGroq();
        _ = services.AddGroqApiKeyCredential("test-key");
        _ = services.AddGroqChatModel(new ModelAlias("chat"), new ModelId("llama-3.3-70b-versatile"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<IChatModel>().ShouldBeOfType<GroqChatModel>();

        model.Alias.ShouldBe(new ModelAlias("chat"));
    }

    private sealed class StaticOAuthTokenProviderRegistration: IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new OAuthTokenProviderCredential("token", null));
    }
}
