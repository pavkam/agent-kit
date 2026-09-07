// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.ZAI.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Verifies the <c>AddZAI*</c> dependency-injection registration surface:
/// endpoint options, credential source selection, and additive model
/// registration.
/// </summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddZAI_WhenNoOptionsConfigured_RegistersDefaultOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddZAI();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ZAIProviderOptions>>().Value;

        options.BaseAddress.ShouldBe(ZAIProviderDefaults.DefaultBaseAddress);
        options.ChatCompletionsPath.ShouldBe(ZAIProviderDefaults.DefaultChatCompletionsPath);
    }

    [Fact]
    public void AddZAI_WhenConfiguredForCodingPlan_UsesCodingPlanBaseAddress()
    {
        var services = new ServiceCollection();
        _ = services.AddZAI(options => options.BaseAddress = ZAIProviderDefaults.CodingPlanBaseAddress);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ZAIProviderOptions>>().Value;

        options.BaseAddress.ShouldBe(ZAIProviderDefaults.CodingPlanBaseAddress);
    }

    [Fact]
    public void AddZAI_WhenBaseAddressIsNotAbsolute_FailsStartupValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddZAI(options => options.BaseAddress = new Uri("not-absolute", UriKind.Relative));

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Theory]
    [InlineData("https://evil.example.test/chat")]
    [InlineData("//evil.example.test/chat")]
    public void AddZAI_WhenChatPathCanReplaceConfiguredEndpoint_FailsStartupValidation(string path)
    {
        var services = new ServiceCollection();
        _ = services.AddZAI(options => options.ChatCompletionsPath = path);

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void AddZAIApiKeyCredential_WhenRegistered_ResolvesApiKeyCredential()
    {
        var services = new ServiceCollection();
        _ = services.AddZAI();
        _ = services.AddZAIApiKeyCredential("zai-test-key");

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(ZAIProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<StaticApiKeyCredentialSource>();
    }

    [Fact]
    public void AddZAIOAuthCredential_WhenRegistered_ResolvesDelegatingOAuthSource()
    {
        var services = new ServiceCollection();
        _ = services.AddZAI();
        _ = services.AddZAIOAuthCredential<StaticOAuthTokenProviderRegistration>();

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(ZAIProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<DelegatingOAuthCredentialSource>();
    }

    [Fact]
    public void AddZAILlmModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddZAI();
        _ = services.AddZAIApiKeyCredential("zai-test-key");
        _ = services.AddZAILlmModel(new ModelAlias("fast"), new ModelId("glm-4.6-flash"));
        _ = services.AddZAILlmModel(new ModelAlias("smart"), new ModelId("glm-4.6"));

        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<ILlmModel>().ToArray();

        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["fast", "smart"], ignoreOrder: true);
    }

    [Fact]
    public void AddZAILlmModel_WhenResolved_UsesZAIProviderIdentity()
    {
        var services = new ServiceCollection();
        _ = services.AddZAI();
        _ = services.AddZAIApiKeyCredential("zai-test-key");
        _ = services.AddZAILlmModel(new ModelAlias("chat"), new ModelId("glm-4.6"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<ZAILlmModel>();

        model.Alias.ShouldBe(new ModelAlias("chat"));
    }

    private sealed class StaticOAuthTokenProviderRegistration: IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new OAuthTokenProviderCredential("token", null));
    }
}
