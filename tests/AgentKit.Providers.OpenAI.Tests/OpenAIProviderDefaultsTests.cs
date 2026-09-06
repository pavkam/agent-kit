// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAI.Tests;

/// <summary>
/// Verifies <see cref="OpenAIProviderDefaults.CreateProfile"/> maps
/// <see cref="OpenAIProviderOptions"/> onto the wire-behavior fields an
/// <see cref="OpenAICompatibilityProfile"/> needs.
/// </summary>
public sealed class OpenAIProviderDefaultsTests
{
    [Fact]
    public void CreateProfile_WhenGivenOptions_MapsEveryField()
    {
        var options = new OpenAIProviderOptions
        {
            BaseAddress = new Uri("https://example.test/"),
            ChatCompletionsPath = "v2/chat",
            PreferStreaming = false,
            IncludeStreamUsage = false,
            UseMaxCompletionTokensField = false,
        };

        var profile = OpenAIProviderDefaults.CreateProfile(options);

        profile.BaseAddress.ShouldBe(options.BaseAddress);
        profile.ChatCompletionsPath.ShouldBe(options.ChatCompletionsPath);
        profile.PreferStreaming.ShouldBeFalse();
        profile.IncludeStreamUsage.ShouldBeFalse();
        profile.UseMaxCompletionTokensField.ShouldBeFalse();
        profile.ChatCompletionsUri.ShouldBe(new Uri("https://example.test/v2/chat"));
    }

    [Fact]
    public void ProviderId_IsStableOpenAIIdentity() => OpenAIProviderDefaults.ProviderId.ShouldBe(new ProviderId("openai"));

    [Fact]
    public void DefaultCapabilities_SupportsToolCallsAndStreaming()
    {
        OpenAIProviderDefaults.DefaultCapabilities.SupportsToolCalls.ShouldBeTrue();
        OpenAIProviderDefaults.DefaultCapabilities.SupportsStreaming.ShouldBeTrue();
        OpenAIProviderDefaults.DefaultCapabilities.SupportsParallelToolCalls.ShouldBeTrue();
    }
}
