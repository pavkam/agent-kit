// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Verifies the <c>AddGoogleGemini*</c> dependency-injection registration
/// surface: endpoint options, credential source selection, and additive
/// model registration.
/// </summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddGoogleGemini_WhenNoOptionsConfigured_RegistersDefaultOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleGemini();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GoogleGeminiProviderOptions>>().Value;

        options.BaseAddress.ShouldBe(GoogleGeminiProviderDefaults.DefaultBaseAddress);
        options.ApiVersion.ShouldBe(GoogleGeminiProviderDefaults.DefaultApiVersion);
        options.PreferStreaming.ShouldBeTrue();
    }

    [Fact]
    public void AddGoogleGemini_WhenBaseAddressIsNotAbsolute_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleGemini(options => options.BaseAddress = new Uri("not-absolute", UriKind.Relative));

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<GoogleGeminiProviderOptions>>().Value);
    }

    [Fact]
    public void AddGoogleGemini_WhenApiVersionIsWhitespace_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleGemini(options => options.ApiVersion = "   ");

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<GoogleGeminiProviderOptions>>().Value);
    }

    [Fact]
    public void AddGoogleGeminiApiKeyCredential_WhenRegistered_ResolvesApiKeyCredential()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleGemini();
        _ = services.AddGoogleGeminiApiKeyCredential("AIza-test-key");

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(GoogleGeminiProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<StaticApiKeyCredentialSource>();
    }

    [Fact]
    public void AddGoogleGeminiOAuthCredential_WhenRegistered_ResolvesDelegatingOAuthSource()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleGemini();
        _ = services.AddGoogleGeminiOAuthCredential<StaticOAuthTokenProviderRegistration>();

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(GoogleGeminiProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<DelegatingOAuthCredentialSource>();
    }

    [Fact]
    public void AddGoogleGeminiLlmModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleGemini();
        _ = services.AddGoogleGeminiApiKeyCredential("AIza-test-key");
        _ = services.AddGoogleGeminiLlmModel(new ModelAlias("fast"), new ModelId("gemini-2.5-flash"));
        _ = services.AddGoogleGeminiLlmModel(new ModelAlias("smart"), new ModelId("gemini-2.5-pro"));

        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<ILlmModel>().ToArray();

        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["fast", "smart"], ignoreOrder: true);
    }

    [Fact]
    public void AddGoogleGeminiLlmModel_WhenResolved_UsesGoogleGeminiProviderIdentity()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleGemini();
        _ = services.AddGoogleGeminiApiKeyCredential("AIza-test-key");
        _ = services.AddGoogleGeminiLlmModel(new ModelAlias("chat"), new ModelId("gemini-2.5-flash"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<GoogleGeminiLlmModel>();

        model.Alias.ShouldBe(new ModelAlias("chat"));
    }

    private sealed class StaticOAuthTokenProviderRegistration: IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new OAuthTokenProviderCredential("token", null));
    }
}
