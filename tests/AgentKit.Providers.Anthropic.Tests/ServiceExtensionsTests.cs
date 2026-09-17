// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Verifies the <c>AddAnthropic*</c> dependency-injection registration
/// surface: endpoint options, credential source selection, and additive
/// model registration.
/// </summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddAnthropic_WhenNoOptionsConfigured_RegistersDefaultOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddAnthropic();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AnthropicProviderOptions>>().Value;

        options.BaseAddress.ShouldBe(AnthropicProviderDefaults.DefaultBaseAddress);
        options.MessagesPath.ShouldBe(AnthropicProviderDefaults.DefaultMessagesPath);
        options.AnthropicVersion.ShouldBe(AnthropicProviderDefaults.DefaultAnthropicVersion);
        options.DefaultMaxOutputTokens.ShouldBe(AnthropicProviderDefaults.DefaultMaxOutputTokensFallback);
    }

    [Fact]
    public void AddAnthropic_WhenRegistered_DisablesTheHttpClientTimeoutInFavorOfThePerRequestDeadline()
    {
        // The BCL default HttpClient.Timeout (100s) would otherwise bound every buffered attempt
        // regardless of the caller's LlmModelRequest.Deadline, since this adapter's own deadlineSource
        // is layered on top of, not instead of, the transport-level timeout.
        var services = new ServiceCollection();
        _ = services.AddAnthropic();

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<HttpClient>();

        client.Timeout.ShouldBe(Timeout.InfiniteTimeSpan);
    }

    [Fact]
    public void AddAnthropic_WhenBaseAddressIsNotAbsolute_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddAnthropic(options => options.BaseAddress = new Uri("not-absolute", UriKind.Relative));

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<AnthropicProviderOptions>>().Value);
    }

    [Fact]
    public void AddAnthropic_WhenDefaultMaxOutputTokensIsNotPositive_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddAnthropic(options => options.DefaultMaxOutputTokens = 0);

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<AnthropicProviderOptions>>().Value);
    }

    [Fact]
    public void AddAnthropicApiKeyCredential_WhenRegistered_ResolvesApiKeyCredential()
    {
        var services = new ServiceCollection();
        _ = services.AddAnthropic();
        _ = services.AddAnthropicApiKeyCredential("sk-ant-test-key");

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(AnthropicProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<StaticApiKeyCredentialSource>();
    }

    [Fact]
    public void AddAnthropicOAuthCredential_WhenRegistered_ResolvesDelegatingOAuthSource()
    {
        var services = new ServiceCollection();
        _ = services.AddAnthropic();
        _ = services.AddAnthropicOAuthCredential<StaticOAuthTokenProviderRegistration>();

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(AnthropicProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<DelegatingOAuthCredentialSource>();
    }

    [Fact]
    public void AddAnthropicLlmModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddAnthropic();
        _ = services.AddAnthropicApiKeyCredential("sk-ant-test-key");
        _ = services.AddAnthropicLlmModel(new ModelAlias("fast"), new ModelId("claude-haiku-4-5"));
        _ = services.AddAnthropicLlmModel(new ModelAlias("smart"), new ModelId("claude-sonnet-4-5"));

        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<ILlmModel>().ToArray();

        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["fast", "smart"], ignoreOrder: true);
    }

    [Fact]
    public void AddAnthropicLlmModel_WhenResolved_UsesAnthropicProviderIdentity()
    {
        var services = new ServiceCollection();
        _ = services.AddAnthropic();
        _ = services.AddAnthropicApiKeyCredential("sk-ant-test-key");
        _ = services.AddAnthropicLlmModel(new ModelAlias("chat"), new ModelId("claude-sonnet-4-5"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<AnthropicLlmModel>();

        model.Alias.ShouldBe(new ModelAlias("chat"));
    }

    private sealed class StaticOAuthTokenProviderRegistration: IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new OAuthTokenProviderCredential("token", null));
    }
}
