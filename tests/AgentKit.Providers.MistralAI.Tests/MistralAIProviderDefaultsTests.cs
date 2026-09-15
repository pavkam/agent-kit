// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Tests;

using AgentKit.Providers.Http;

/// <summary>Verifies MistralAIProviderDefaults behavior and contracts.</summary>
public sealed class MistralAIProviderDefaultsTests
{
    [Fact]
    public void BuildChatCompletionsUri_WhenGivenOptions_CombinesBaseAddressAndPath()
    {
        var options = new MistralAIProviderOptions
        {
            BaseAddress = new Uri("https://example.test/"),
            ChatCompletionsPath = "v2/chat/completions",
        };
        MistralAIProviderDefaults.BuildChatCompletionsUri(options).ShouldBe(new Uri("https://example.test/v2/chat/completions"));
    }

    [Fact]
    public void BuildEmbeddingsUri_WhenGivenOptions_CombinesBaseAddressAndPath()
    {
        var options = new MistralAIProviderOptions
        {
            BaseAddress = new Uri("https://example.test/"),
            EmbeddingsPath = "v2/embeddings",
        };
        MistralAIProviderDefaults.BuildEmbeddingsUri(options).ShouldBe(new Uri("https://example.test/v2/embeddings"));
    }

    [Fact]
    public void EmbeddingApiFamily_IsStableMistralAIEmbeddingsIdentity() => MistralAIProviderDefaults.EmbeddingApiFamily.ShouldBe(new ApiFamilyId("mistral-embeddings"));
    [Fact]
    public void DefaultEmbeddingCapabilities_SupportsEncodingSelectionAndDimensionsButNotPurpose()
    {
        MistralAIProviderDefaults.DefaultEmbeddingCapabilities.SupportsEncodingSelection.ShouldBeTrue();
        MistralAIProviderDefaults.DefaultEmbeddingCapabilities.SupportsDimensions.ShouldBeTrue();
        MistralAIProviderDefaults.DefaultEmbeddingCapabilities.SupportsPurpose.ShouldBeFalse();
    }

    [Fact]
    public void DefaultCapabilities_DoesNotAdvertiseReasoningOrVisionOrStructuredOutput()
    {
        MistralAIProviderDefaults.DefaultCapabilities.SupportsReasoning.ShouldBeFalse();
        MistralAIProviderDefaults.DefaultCapabilities.SupportsVisionInput.ShouldBeFalse();
        MistralAIProviderDefaults.DefaultCapabilities.SupportsStructuredOutput.ShouldBeFalse();
        MistralAIProviderDefaults.DefaultCapabilities.SupportsParallelToolCalls.ShouldBeTrue();
        MistralAIProviderDefaults.DefaultCapabilities.SupportsToolCalls.ShouldBeTrue();
    }

    [Fact]
    public void AuthorizationScheme_WhenApiKeyCredential_ResolvesToAuthorizationBearerHeader()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero));

        var result = ProviderAuthorizationHeaderFactory.Create(
            new ApiKeyProviderCredential("mistral-test-key"),
            MistralAIProviderDefaults.ProviderId,
            clock,
            MistralAIProviderDefaults.AuthorizationScheme);

        var granted = result.ShouldBeOfType<ProviderAuthorizationGranted>();
        granted.HeaderName.ShouldBe("Authorization");
        granted.HeaderValue.ShouldBe("Bearer mistral-test-key");
    }
}
