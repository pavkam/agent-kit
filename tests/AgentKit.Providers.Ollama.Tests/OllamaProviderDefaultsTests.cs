// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Ollama.Tests;

/// <summary>
/// Verifies <see cref="OllamaProviderDefaults.CreateProfile"/> maps
/// <see cref="OllamaProviderOptions"/> onto the wire-behavior fields an
/// <see cref="OpenAICompatibilityProfile"/> needs.
/// </summary>
public sealed class OllamaProviderDefaultsTests
{
    [Fact]
    public void CreateProfile_WhenGivenOptions_MapsEveryField()
    {
        var options = new OllamaProviderOptions
        {
            BaseAddress = new Uri("https://example.test/"),
            ChatCompletionsPath = "v2/chat",
            PreferStreaming = false,
            IncludeStreamUsage = false,
        };

        var profile = OllamaProviderDefaults.CreateProfile(options);

        profile.BaseAddress.ShouldBe(options.BaseAddress);
        profile.ChatCompletionsPath.ShouldBe(options.ChatCompletionsPath);
        profile.PreferStreaming.ShouldBeFalse();
        profile.IncludeStreamUsage.ShouldBeFalse();
        profile.UseMaxCompletionTokensField.ShouldBeFalse();
        profile.SendDeveloperRoleAsSystem.ShouldBeTrue();
        profile.ChatCompletionsUri.ShouldBe(new Uri("https://example.test/v2/chat"));
        profile.EmbeddingsUri.ShouldBe(new Uri("https://example.test/embeddings"));
    }

    [Fact]
    public void ProviderId_IsStableOllamaIdentity() =>
        OllamaProviderDefaults.ProviderId.ShouldBe(new ProviderId("ollama"));

    [Fact]
    public void EmbeddingApiFamily_IsStableOllamaEmbeddingsIdentity() =>
        OllamaProviderDefaults.EmbeddingApiFamily.ShouldBe(new ApiFamilyId("ollama-openai-compatible-embeddings"));
}
