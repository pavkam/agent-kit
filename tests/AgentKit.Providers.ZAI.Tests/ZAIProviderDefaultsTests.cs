// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.ZAI.Tests;



/// <summary>Verifies ZAIProviderDefaults behavior and contracts.</summary>
public sealed class ZAIProviderDefaultsTests
{
    [Fact]
    public void CreateProfile_WhenGivenOptions_MapsEveryField()
    {
        var options = new ZAIProviderOptions
        {
            BaseAddress = new Uri("https://example.test/"),
            ChatCompletionsPath = "v2/chat",
            PreferStreaming = false,
            IncludeStreamUsage = false,
        };
        var profile = ZAIProviderDefaults.CreateProfile(options);
        profile.BaseAddress.ShouldBe(options.BaseAddress);
        profile.ChatCompletionsPath.ShouldBe(options.ChatCompletionsPath);
        profile.PreferStreaming.ShouldBeFalse();
        profile.IncludeStreamUsage.ShouldBeFalse();
        profile.UseMaxCompletionTokensField.ShouldBeFalse();
        profile.SendDeveloperRoleAsSystem.ShouldBeTrue();
        profile.ChatCompletionsUri.ShouldBe(new Uri("https://example.test/v2/chat"));
    }

    [Fact]
    public void DefaultCapabilities_DoesNotClaimParallelToolCalls() => ZAIProviderDefaults.DefaultCapabilities.SupportsParallelToolCalls.ShouldBeFalse();
    [Fact]
    public void DefaultBaseAddress_DiffersFromCodingPlanBaseAddress() => ZAIProviderDefaults.DefaultBaseAddress.ShouldNotBe(ZAIProviderDefaults.CodingPlanBaseAddress);
}
