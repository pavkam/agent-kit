// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Verifies the <c>AddCohere*</c> dependency-injection registration
/// surface: endpoint options, credential source selection, and additive
/// model registration.
/// </summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddCohere_WhenNoOptionsConfigured_RegistersDefaultOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddCohere();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<CohereProviderOptions>>().Value;

        options.BaseAddress.ShouldBe(CohereProviderDefaults.DefaultBaseAddress);
        options.ChatPath.ShouldBe(CohereProviderDefaults.DefaultChatPath);
        options.PreferStreaming.ShouldBeTrue();
        options.ClientName.ShouldBeNull();
    }

    [Fact]
    public void AddCohere_WhenBaseAddressIsNotAbsolute_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddCohere(options => options.BaseAddress = new Uri("not-absolute", UriKind.Relative));

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<CohereProviderOptions>>().Value);
    }

    [Fact]
    public void AddCohere_WhenChatPathIsWhitespace_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddCohere(options => options.ChatPath = "   ");

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<CohereProviderOptions>>().Value);
    }

    [Fact]
    public void AddCohereApiKeyCredential_WhenRegistered_ResolvesApiKeyCredential()
    {
        var services = new ServiceCollection();
        _ = services.AddCohere();
        _ = services.AddCohereApiKeyCredential("cohere-test-key");

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(CohereProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<StaticApiKeyCredentialSource>();
    }

    [Fact]
    public void AddCohereOAuthCredential_WhenRegistered_ResolvesDelegatingOAuthSource()
    {
        var services = new ServiceCollection();
        _ = services.AddCohere();
        _ = services.AddCohereOAuthCredential<StaticOAuthTokenProviderRegistration>();

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(CohereProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<DelegatingOAuthCredentialSource>();
    }

    [Fact]
    public void AddCohereLlmModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddCohere();
        _ = services.AddCohereApiKeyCredential("cohere-test-key");
        _ = services.AddCohereLlmModel(new ModelAlias("fast"), new ModelId("command-a-05-2026"));
        _ = services.AddCohereLlmModel(new ModelAlias("smart"), new ModelId("command-a-plus-05-2026"));

        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<ILlmModel>().ToArray();

        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["fast", "smart"], ignoreOrder: true);
    }

    [Fact]
    public void AddCohereLlmModel_WhenResolved_UsesCohereProviderIdentity()
    {
        var services = new ServiceCollection();
        _ = services.AddCohere();
        _ = services.AddCohereApiKeyCredential("cohere-test-key");
        _ = services.AddCohereLlmModel(new ModelAlias("chat"), new ModelId("command-a-plus-05-2026"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<CohereLlmModel>();

        model.Alias.ShouldBe(new ModelAlias("chat"));
    }

    private sealed class StaticOAuthTokenProviderRegistration: IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new OAuthTokenProviderCredential("token", null));
    }
}
