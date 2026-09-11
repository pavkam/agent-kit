// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Tests;



/// <summary>Verifies GoogleGeminiProviderDefaults behavior and contracts.</summary>
public sealed class GoogleGeminiProviderDefaultsTests
{
    [Fact]
    public void BuildGenerateContentUri_WhenNotStreaming_BuildsGenerateContentOperationUri()
    {
        var options = new GoogleGeminiProviderOptions
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var uri = GoogleGeminiProviderDefaults.BuildGenerateContentUri(options, new ModelId("gemini-2.5-flash"), useStreaming: false);
        uri.ShouldBe(new Uri("https://example.test/v1beta/models/gemini-2.5-flash:generateContent"));
    }

    [Fact]
    public void BuildGenerateContentUri_WhenStreaming_BuildsStreamGenerateContentOperationUriWithSseQuery()
    {
        var options = new GoogleGeminiProviderOptions
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var uri = GoogleGeminiProviderDefaults.BuildGenerateContentUri(options, new ModelId("gemini-2.5-flash"), useStreaming: true);
        uri.ShouldBe(new Uri("https://example.test/v1beta/models/gemini-2.5-flash:streamGenerateContent?alt=sse"));
    }

    [Fact]
    public void BuildGenerateContentUri_WhenCustomApiVersion_UsesConfiguredVersionSegment()
    {
        var options = new GoogleGeminiProviderOptions
        {
            BaseAddress = new Uri("https://example.test/"),
            ApiVersion = "v1"
        };
        var uri = GoogleGeminiProviderDefaults.BuildGenerateContentUri(options, new ModelId("gemini-2.5-pro"), useStreaming: false);
        uri.ShouldBe(new Uri("https://example.test/v1/models/gemini-2.5-pro:generateContent"));
    }

    [Fact]
    public void BuildBatchEmbedContentsUri_WhenGivenOptions_BuildsBatchEmbedContentsOperationUri()
    {
        var options = new GoogleGeminiProviderOptions
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var uri = GoogleGeminiProviderDefaults.BuildBatchEmbedContentsUri(options, new ModelId("text-embedding-004"));
        uri.ShouldBe(new Uri("https://example.test/v1beta/models/text-embedding-004:batchEmbedContents"));
    }

    [Fact]
    public void EmbeddingApiFamily_IsStableGoogleGeminiEmbeddingsIdentity() => GoogleGeminiProviderDefaults.EmbeddingApiFamily.ShouldBe(new ApiFamilyId("google-gemini-embed-content"));
    [Fact]
    public void DefaultCapabilities_SupportsReasoningButNotVisionOrStructuredOutput()
    {
        GoogleGeminiProviderDefaults.DefaultCapabilities.SupportsReasoning.ShouldBeTrue();
        GoogleGeminiProviderDefaults.DefaultCapabilities.SupportsVisionInput.ShouldBeFalse();
        GoogleGeminiProviderDefaults.DefaultCapabilities.SupportsStructuredOutput.ShouldBeFalse();
        GoogleGeminiProviderDefaults.DefaultCapabilities.SupportsParallelToolCalls.ShouldBeTrue();
    }

    [Fact]
    public void DefaultEmbeddingCapabilities_SupportsPurposeAndTruncationControlButNotEncodingSelection()
    {
        GoogleGeminiProviderDefaults.DefaultEmbeddingCapabilities.SupportsPurpose.ShouldBeTrue();
        GoogleGeminiProviderDefaults.DefaultEmbeddingCapabilities.SupportsTruncationControl.ShouldBeTrue();
        GoogleGeminiProviderDefaults.DefaultEmbeddingCapabilities.SupportsEncodingSelection.ShouldBeFalse();
        GoogleGeminiProviderDefaults.DefaultEmbeddingCapabilities.SupportsDimensions.ShouldBeTrue();
    }
}
