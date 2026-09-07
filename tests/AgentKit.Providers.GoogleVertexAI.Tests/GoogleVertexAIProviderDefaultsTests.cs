// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI.Tests;

/// <summary>
/// Verifies <see cref="GoogleVertexAIProviderDefaults.BuildGenerateContentUri"/>
/// and the shared provider identity/capability defaults.
/// </summary>
public sealed class GoogleVertexAIProviderDefaultsTests
{
    private static GoogleVertexAIProviderOptions CreateOptions() => new()
    {
        ProjectId = "my-project",
        Location = "us-central1",
    };

    [Fact]
    public void BuildGenerateContentUri_WhenNotStreamingAndNoDeployment_UsesPublisherModelResource()
    {
        var options = CreateOptions();

        var uri = GoogleVertexAIProviderDefaults.BuildGenerateContentUri(
            options, new ModelId("gemini-2.5-flash"), deploymentId: null, useStreaming: false);

        uri.ShouldBe(new Uri(
            "https://us-central1-aiplatform.googleapis.com/v1/projects/my-project/locations/us-central1/" +
            "publishers/google/models/gemini-2.5-flash:generateContent"));
    }

    [Fact]
    public void BuildGenerateContentUri_WhenStreaming_AppendsAltSseQuery()
    {
        var options = CreateOptions();

        var uri = GoogleVertexAIProviderDefaults.BuildGenerateContentUri(
            options, new ModelId("gemini-2.5-flash"), deploymentId: null, useStreaming: true);

        uri.ShouldBe(new Uri(
            "https://us-central1-aiplatform.googleapis.com/v1/projects/my-project/locations/us-central1/" +
            "publishers/google/models/gemini-2.5-flash:streamGenerateContent?alt=sse"));
    }

    [Fact]
    public void BuildGenerateContentUri_WhenDeploymentIdSupplied_UsesEndpointResourceInstead()
    {
        var options = CreateOptions();

        var uri = GoogleVertexAIProviderDefaults.BuildGenerateContentUri(
            options, new ModelId("gemini-2.5-flash"), new DeploymentId("my-endpoint"), useStreaming: false);

        uri.ShouldBe(new Uri(
            "https://us-central1-aiplatform.googleapis.com/v1/projects/my-project/locations/us-central1/" +
            "endpoints/my-endpoint:generateContent"));
    }

    [Fact]
    public void BuildGenerateContentUri_WhenCustomPublisherAndApiVersion_UsesConfiguredValues()
    {
        var options = CreateOptions();
        options.Publisher = "anthropic";
        options.ApiVersion = "v1beta1";

        var uri = GoogleVertexAIProviderDefaults.BuildGenerateContentUri(
            options, new ModelId("claude-opus-4"), deploymentId: null, useStreaming: false);

        uri.ShouldBe(new Uri(
            "https://us-central1-aiplatform.googleapis.com/v1beta1/projects/my-project/locations/us-central1/" +
            "publishers/anthropic/models/claude-opus-4:generateContent"));
    }

    [Fact]
    public void ProviderId_IsStableGoogleVertexAIIdentity() =>
        GoogleVertexAIProviderDefaults.ProviderId.ShouldBe(new ProviderId("google-vertex-ai"));

    [Fact]
    public void DefaultCapabilities_MatchesGeminiDefaults() =>
        GoogleVertexAIProviderDefaults.DefaultCapabilities.ShouldBe(GoogleGeminiProviderDefaults.DefaultCapabilities);
}
