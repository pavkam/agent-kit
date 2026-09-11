// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Groq.Tests;



/// <summary>Verifies GroqProviderDefaults behavior and contracts.</summary>
public sealed class GroqProviderDefaultsTests
{
    [Fact]
    public void CreateProfile_WhenGivenOptions_MapsEveryField()
    {
        var options = new GroqProviderOptions
        {
            BaseAddress = new Uri("https://example.test/"),
            ChatCompletionsPath = "v2/chat",
            PreferStreaming = false,
            IncludeStreamUsage = false,
        };
        var profile = GroqProviderDefaults.CreateProfile(options);
        profile.BaseAddress.ShouldBe(options.BaseAddress);
        profile.ChatCompletionsPath.ShouldBe(options.ChatCompletionsPath);
        profile.PreferStreaming.ShouldBeFalse();
        profile.IncludeStreamUsage.ShouldBeFalse();
        profile.UseMaxCompletionTokensField.ShouldBeFalse();
        profile.SendDeveloperRoleAsSystem.ShouldBeTrue();
        profile.ChatCompletionsUri.ShouldBe(new Uri("https://example.test/v2/chat"));
    }
}
