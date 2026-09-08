// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AzureOpenAI.Tests;

/// <summary>
/// Verifies <see cref="AzureOpenAIProviderDefaults.CreateProfile"/> maps
/// <see cref="AzureOpenAIProviderOptions"/> onto the wire-behavior fields
/// an <see cref="OpenAICompatibilityProfile"/> needs.
/// </summary>
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
        var options = new AzureOpenAIProviderOptions { ResourceEndpoint = null };

        _ = Should.Throw<ArgumentNullException>(() => AzureOpenAIProviderDefaults.CreateProfile(options));
    }

    [Fact]
    public void ProviderId_IsStableAzureOpenAIIdentity() =>
        AzureOpenAIProviderDefaults.ProviderId.ShouldBe(new ProviderId("azure-openai"));

    [Fact]
    public void EmbeddingApiFamily_IsStableAzureOpenAIEmbeddingsIdentity() =>
        AzureOpenAIProviderDefaults.EmbeddingApiFamily.ShouldBe(new ApiFamilyId("azure-openai-embeddings"));

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
}
