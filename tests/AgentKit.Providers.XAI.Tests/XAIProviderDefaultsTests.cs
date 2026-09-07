// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.XAI.Tests;

/// <summary>
/// Verifies <see cref="XAIProviderDefaults.CreateProfile"/> maps
/// <see cref="XAIProviderOptions"/> onto the wire-behavior fields an
/// <see cref="OpenAICompatibilityProfile"/> needs.
/// </summary>
public sealed class XAIProviderDefaultsTests
{
    [Fact]
    public void CreateProfile_WhenGivenOptions_MapsEveryField()
    {
        var options = new XAIProviderOptions
        {
            BaseAddress = new Uri("https://example.test/"),
            ChatCompletionsPath = "v2/chat",
            PreferStreaming = false,
            IncludeStreamUsage = false,
        };

        var profile = XAIProviderDefaults.CreateProfile(options);

        profile.BaseAddress.ShouldBe(options.BaseAddress);
        profile.ChatCompletionsPath.ShouldBe(options.ChatCompletionsPath);
        profile.PreferStreaming.ShouldBeFalse();
        profile.IncludeStreamUsage.ShouldBeFalse();
        profile.UseMaxCompletionTokensField.ShouldBeFalse();
        profile.SendDeveloperRoleAsSystem.ShouldBeTrue();
        profile.ChatCompletionsUri.ShouldBe(new Uri("https://example.test/v2/chat"));
    }

    [Fact]
    public void ProviderId_IsStableXAIIdentity() =>
        XAIProviderDefaults.ProviderId.ShouldBe(new ProviderId("xai"));
}
