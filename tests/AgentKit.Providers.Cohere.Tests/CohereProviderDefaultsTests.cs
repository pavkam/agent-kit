// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Tests;

using AgentKit.Providers.Http;

/// <summary>Verifies CohereProviderDefaults behavior and contracts.</summary>
public sealed class CohereProviderDefaultsTests
{
    [Fact]
    public void BuildChatUri_WhenGivenOptions_CombinesBaseAddressAndPath()
    {
        var options = new CohereProviderOptions
        {
            BaseAddress = new Uri("https://example.test/"),
            ChatPath = "v3/chat",
        };
        CohereProviderDefaults.BuildChatUri(options).ShouldBe(new Uri("https://example.test/v3/chat"));
    }

    [Fact]
    public void DefaultCapabilities_SupportsReasoningButNotVisionOrStructuredOutput()
    {
        CohereProviderDefaults.DefaultCapabilities.SupportsReasoning.ShouldBeTrue();
        CohereProviderDefaults.DefaultCapabilities.SupportsVisionInput.ShouldBeFalse();
        CohereProviderDefaults.DefaultCapabilities.SupportsStructuredOutput.ShouldBeFalse();
        CohereProviderDefaults.DefaultCapabilities.SupportsParallelToolCalls.ShouldBeTrue();
        CohereProviderDefaults.DefaultCapabilities.SupportsToolCalls.ShouldBeTrue();
    }

    [Fact]
    public void BuildEmbedUri_WhenGivenOptions_CombinesBaseAddressAndPath()
    {
        var options = new CohereProviderOptions
        {
            BaseAddress = new Uri("https://example.test/"),
            EmbedPath = "v3/embed",
        };
        CohereProviderDefaults.BuildEmbedUri(options).ShouldBe(new Uri("https://example.test/v3/embed"));
    }

    [Fact]
    public void EmbeddingApiFamily_IsStableCohereEmbedIdentity() => CohereProviderDefaults.EmbeddingApiFamily.ShouldBe(new ApiFamilyId("cohere-embed-v2"));
    [Fact]
    public void DefaultEmbeddingCapabilities_SupportsBatchDimensionsPurposeEncodingAndTruncation()
    {
        CohereProviderDefaults.DefaultEmbeddingCapabilities.SupportsBatchInput.ShouldBeTrue();
        CohereProviderDefaults.DefaultEmbeddingCapabilities.SupportsDimensions.ShouldBeTrue();
        CohereProviderDefaults.DefaultEmbeddingCapabilities.SupportsPurpose.ShouldBeTrue();
        CohereProviderDefaults.DefaultEmbeddingCapabilities.SupportsEncodingSelection.ShouldBeTrue();
        CohereProviderDefaults.DefaultEmbeddingCapabilities.SupportsTruncationControl.ShouldBeTrue();
    }

    [Fact]
    public void DefaultEmbeddingLimits_CapsInputsAtNinetySix() => CohereProviderDefaults.DefaultEmbeddingLimits.MaxInputsPerRequest.ShouldBe(96);

    [Fact]
    public void AuthorizationScheme_WhenApiKeyCredential_ResolvesToAuthorizationBearerHeader()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero));

        var result = ProviderAuthorizationHeaderFactory.Create(
            new ApiKeyProviderCredential("co-test-key"),
            CohereProviderDefaults.ProviderId,
            clock,
            CohereProviderDefaults.AuthorizationScheme);

        var granted = result.ShouldBeOfType<ProviderAuthorizationGranted>();
        granted.HeaderName.ShouldBe("Authorization");
        granted.HeaderValue.ShouldBe("Bearer co-test-key");
    }
}
