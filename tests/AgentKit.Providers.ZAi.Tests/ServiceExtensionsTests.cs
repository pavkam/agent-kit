// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.ZAi.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Verifies the <c>AddZAi*</c> dependency-injection registration surface:
/// endpoint options, credential source selection, and additive model
/// registration.
/// </summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddZAi_WhenNoOptionsConfigured_RegistersDefaultOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddZAi();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ZAiProviderOptions>>().Value;

        options.BaseAddress.ShouldBe(ZAiProviderDefaults.DefaultBaseAddress);
        options.ChatCompletionsPath.ShouldBe(ZAiProviderDefaults.DefaultChatCompletionsPath);
    }

    [Fact]
    public void AddZAi_WhenConfiguredForCodingPlan_UsesCodingPlanBaseAddress()
    {
        var services = new ServiceCollection();
        _ = services.AddZAi(options => options.BaseAddress = ZAiProviderDefaults.CodingPlanBaseAddress);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ZAiProviderOptions>>().Value;

        options.BaseAddress.ShouldBe(ZAiProviderDefaults.CodingPlanBaseAddress);
    }

    [Fact]
    public void AddZAi_WhenBaseAddressIsNotAbsolute_FailsStartupValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddZAi(options => options.BaseAddress = new Uri("not-absolute", UriKind.Relative));

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Theory]
    [InlineData("https://evil.example.test/chat")]
    [InlineData("//evil.example.test/chat")]
    public void AddZAi_WhenChatPathCanReplaceConfiguredEndpoint_FailsStartupValidation(string path)
    {
        var services = new ServiceCollection();
        _ = services.AddZAi(options => options.ChatCompletionsPath = path);

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void AddZAiApiKeyCredential_WhenRegistered_ResolvesApiKeyCredential()
    {
        var services = new ServiceCollection();
        _ = services.AddZAi();
        _ = services.AddZAiApiKeyCredential("zai-test-key");

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(ZAiProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<StaticApiKeyCredentialSource>();
    }

    [Fact]
    public void AddZAiOAuthCredential_WhenRegistered_ResolvesDelegatingOAuthSource()
    {
        var services = new ServiceCollection();
        _ = services.AddZAi();
        _ = services.AddZAiOAuthCredential<StaticOAuthTokenProviderRegistration>();

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(ZAiProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<DelegatingOAuthCredentialSource>();
    }

    [Fact]
    public void AddZAiChatModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddZAi();
        _ = services.AddZAiApiKeyCredential("zai-test-key");
        _ = services.AddZAiChatModel(new ModelAlias("fast"), new ModelId("glm-4.6-flash"));
        _ = services.AddZAiChatModel(new ModelAlias("smart"), new ModelId("glm-4.6"));

        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<IChatModel>().ToArray();

        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["fast", "smart"], ignoreOrder: true);
    }

    [Fact]
    public void AddZAiChatModel_WhenResolved_UsesZAiProviderIdentity()
    {
        var services = new ServiceCollection();
        _ = services.AddZAi();
        _ = services.AddZAiApiKeyCredential("zai-test-key");
        _ = services.AddZAiChatModel(new ModelAlias("chat"), new ModelId("glm-4.6"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<IChatModel>().ShouldBeOfType<ZAiChatModel>();

        model.Alias.ShouldBe(new ModelAlias("chat"));
    }

    private sealed class StaticOAuthTokenProviderRegistration: IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new OAuthTokenProviderCredential("token", null));
    }
}
