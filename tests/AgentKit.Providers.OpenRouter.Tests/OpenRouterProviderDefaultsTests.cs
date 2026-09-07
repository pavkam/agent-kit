// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenRouter.Tests;

/// <summary>
/// Verifies <see cref="OpenRouterProviderDefaults.CreateProfile"/> maps
/// <see cref="OpenRouterProviderOptions"/> onto the wire-behavior fields an
/// <see cref="OpenAICompatibilityProfile"/> needs, including OpenRouter's
/// optional attribution and routing-metadata headers.
/// </summary>
public sealed class OpenRouterProviderDefaultsTests
{
    [Fact]
    public void CreateProfile_WhenNoAttributionConfigured_HasNoAdditionalHeaders()
    {
        var profile = OpenRouterProviderDefaults.CreateProfile(new OpenRouterProviderOptions());

        profile.DefaultRequestHeaders.ShouldBeEmpty();
        profile.UseMaxCompletionTokensField.ShouldBeFalse();
    }

    [Fact]
    public void CreateProfile_WhenAttributionConfigured_AddsDocumentedHeaders()
    {
        var options = new OpenRouterProviderOptions
        {
            HttpReferer = "https://example.test/",
            ApplicationTitle = "My App",
            IncludeRoutingMetadata = true,
        };

        var profile = OpenRouterProviderDefaults.CreateProfile(options);

        profile.DefaultRequestHeaders["HTTP-Referer"].ShouldBe("https://example.test/");
        profile.DefaultRequestHeaders["X-OpenRouter-Title"].ShouldBe("My App");
        profile.DefaultRequestHeaders["X-OpenRouter-Metadata"].ShouldBe("enabled");
    }

    [Fact]
    public void CreateProfile_WhenGivenOptions_MapsEndpointFields()
    {
        var options = new OpenRouterProviderOptions
        {
            BaseAddress = new Uri("https://example.test/"),
            ChatCompletionsPath = "v2/chat",
            PreferStreaming = false,
            IncludeStreamUsage = false,
        };

        var profile = OpenRouterProviderDefaults.CreateProfile(options);

        profile.BaseAddress.ShouldBe(options.BaseAddress);
        profile.ChatCompletionsPath.ShouldBe(options.ChatCompletionsPath);
        profile.PreferStreaming.ShouldBeFalse();
        profile.IncludeStreamUsage.ShouldBeFalse();
        profile.ChatCompletionsUri.ShouldBe(new Uri("https://example.test/v2/chat"));
    }

    [Fact]
    public void ProviderId_IsStableOpenRouterIdentity() =>
        OpenRouterProviderDefaults.ProviderId.ShouldBe(new ProviderId("openrouter"));
}
