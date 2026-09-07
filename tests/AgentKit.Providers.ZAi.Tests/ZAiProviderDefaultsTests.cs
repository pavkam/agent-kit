// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.ZAi.Tests;

/// <summary>
/// Verifies <see cref="ZAiProviderDefaults.CreateProfile"/> maps
/// <see cref="ZAiProviderOptions"/> onto the wire-behavior fields an
/// <see cref="OpenAICompatibilityProfile"/> needs.
/// </summary>
public sealed class ZAiProviderDefaultsTests
{
    [Fact]
    public void CreateProfile_WhenGivenOptions_MapsEveryField()
    {
        var options = new ZAiProviderOptions
        {
            BaseAddress = new Uri("https://example.test/"),
            ChatCompletionsPath = "v2/chat",
            PreferStreaming = false,
            IncludeStreamUsage = false,
        };

        var profile = ZAiProviderDefaults.CreateProfile(options);

        profile.BaseAddress.ShouldBe(options.BaseAddress);
        profile.ChatCompletionsPath.ShouldBe(options.ChatCompletionsPath);
        profile.PreferStreaming.ShouldBeFalse();
        profile.IncludeStreamUsage.ShouldBeFalse();
        profile.UseMaxCompletionTokensField.ShouldBeFalse();
        profile.SendDeveloperRoleAsSystem.ShouldBeTrue();
        profile.ChatCompletionsUri.ShouldBe(new Uri("https://example.test/v2/chat"));
    }

    [Fact]
    public void ProviderId_IsStableZAiIdentity() => ZAiProviderDefaults.ProviderId.ShouldBe(new ProviderId("z-ai"));

    [Fact]
    public void DefaultCapabilities_DoesNotClaimParallelToolCalls() =>
        ZAiProviderDefaults.DefaultCapabilities.SupportsParallelToolCalls.ShouldBeFalse();

    [Fact]
    public void DefaultBaseAddress_DiffersFromCodingPlanBaseAddress() =>
        ZAiProviderDefaults.DefaultBaseAddress.ShouldNotBe(ZAiProviderDefaults.CodingPlanBaseAddress);
}
