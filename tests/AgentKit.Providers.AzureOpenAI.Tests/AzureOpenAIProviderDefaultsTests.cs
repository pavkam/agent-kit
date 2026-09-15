// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AzureOpenAI.Tests;

using AgentKit.Providers.Http;

/// <summary>Verifies AzureOpenAIProviderDefaults behavior and contracts.</summary>
public sealed class AzureOpenAIProviderDefaultsTests
{
    [Fact]
    public void CreateProfile_WhenGivenOptions_MapsEveryField()
    {
        var options = new AzureOpenAIProviderOptions
        {
            ResourceEndpoint = new Uri("https://my-resource.openai.azure.test/"),
            ChatCompletionsPath = "openai/v1/chat/completions",
            PreferStreaming = false,
            IncludeStreamUsage = false,
            UseMaxCompletionTokensField = false,
        };
        var profile = AzureOpenAIProviderDefaults.CreateProfile(options);
        profile.BaseAddress.ShouldBe(options.ResourceEndpoint);
        profile.ChatCompletionsPath.ShouldBe(options.ChatCompletionsPath);
        profile.PreferStreaming.ShouldBeFalse();
        profile.IncludeStreamUsage.ShouldBeFalse();
        profile.UseMaxCompletionTokensField.ShouldBeFalse();
        profile.ChatCompletionsUri.ShouldBe(new Uri("https://my-resource.openai.azure.test/openai/v1/chat/completions"));
        profile.EmbeddingsUri.ShouldBe(new Uri("https://my-resource.openai.azure.test/openai/v1/embeddings"));
    }

    [Fact]
    public void CreateProfile_WhenResourceEndpointIsNull_ThrowsArgumentNullException()
    {
        var options = new AzureOpenAIProviderOptions
        {
            ResourceEndpoint = null
        };
        _ = Should.Throw<ArgumentNullException>(() => AzureOpenAIProviderDefaults.CreateProfile(options));
    }

    [Fact]
    public void EmbeddingApiFamily_IsStableAzureOpenAIEmbeddingsIdentity() => AzureOpenAIProviderDefaults.EmbeddingApiFamily.ShouldBe(new ApiFamilyId("azure-openai-embeddings"));
    [Fact]
    public void DefaultCapabilities_SupportsToolCallsAndStreaming()
    {
        AzureOpenAIProviderDefaults.DefaultCapabilities.SupportsToolCalls.ShouldBeTrue();
        AzureOpenAIProviderDefaults.DefaultCapabilities.SupportsStreaming.ShouldBeTrue();
        AzureOpenAIProviderDefaults.DefaultCapabilities.SupportsParallelToolCalls.ShouldBeTrue();
    }

    [Fact]
    public void DefaultEmbeddingCapabilities_SupportsDimensionsAndEncodingSelection()
    {
        AzureOpenAIProviderDefaults.DefaultEmbeddingCapabilities.SupportsDimensions.ShouldBeTrue();
        AzureOpenAIProviderDefaults.DefaultEmbeddingCapabilities.SupportsEncodingSelection.ShouldBeTrue();
        AzureOpenAIProviderDefaults.DefaultEmbeddingCapabilities.SupportsPurpose.ShouldBeFalse();
    }

    [Fact]
    public void AuthorizationScheme_WhenApiKeyCredential_ResolvesToapikeyHeader()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero));

        var result = ProviderAuthorizationHeaderFactory.Create(
            new ApiKeyProviderCredential("azure-key"),
            AzureOpenAIProviderDefaults.ProviderId,
            clock,
            AzureOpenAIProviderDefaults.AuthorizationScheme);

        var granted = result.ShouldBeOfType<ProviderAuthorizationGranted>();
        granted.HeaderName.ShouldBe("api-key");
        granted.HeaderValue.ShouldBe("azure-key");
    }
}
