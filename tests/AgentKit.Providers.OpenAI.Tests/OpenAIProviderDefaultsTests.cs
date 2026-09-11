// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAI.Tests;



/// <summary>Verifies OpenAIProviderDefaults behavior and contracts.</summary>
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
        profile.EmbeddingsUri.ShouldBe(new Uri("https://example.test/v1/embeddings"));
    }

    [Fact]
    public void CreateProfile_WhenEmbeddingsPathOverridden_MapsEmbeddingsUri()
    {
        var options = new OpenAIProviderOptions
        {
            BaseAddress = new Uri("https://example.test/"),
            EmbeddingsPath = "v2/embeddings",
        };
        var profile = OpenAIProviderDefaults.CreateProfile(options);
        profile.EmbeddingsUri.ShouldBe(new Uri("https://example.test/v2/embeddings"));
    }

    [Fact]
    public void EmbeddingApiFamily_IsStableOpenAIEmbeddingsIdentity() => OpenAIProviderDefaults.EmbeddingApiFamily.ShouldBe(new ApiFamilyId("openai-embeddings"));
    [Fact]
    public void DefaultCapabilities_SupportsToolCallsAndStreaming()
    {
        OpenAIProviderDefaults.DefaultCapabilities.SupportsToolCalls.ShouldBeTrue();
        OpenAIProviderDefaults.DefaultCapabilities.SupportsStreaming.ShouldBeTrue();
        OpenAIProviderDefaults.DefaultCapabilities.SupportsParallelToolCalls.ShouldBeTrue();
    }

    [Fact]
    public void DefaultEmbeddingCapabilities_SupportsDimensionsAndEncodingSelectionButNotPurpose()
    {
        OpenAIProviderDefaults.DefaultEmbeddingCapabilities.SupportsDimensions.ShouldBeTrue();
        OpenAIProviderDefaults.DefaultEmbeddingCapabilities.SupportsEncodingSelection.ShouldBeTrue();
        OpenAIProviderDefaults.DefaultEmbeddingCapabilities.SupportsBatchInput.ShouldBeTrue();
        OpenAIProviderDefaults.DefaultEmbeddingCapabilities.SupportsPurpose.ShouldBeFalse();
        OpenAIProviderDefaults.DefaultEmbeddingCapabilities.SupportsTruncationControl.ShouldBeFalse();
    }
}
