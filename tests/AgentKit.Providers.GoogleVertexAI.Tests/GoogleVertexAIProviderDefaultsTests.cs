// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI.Tests;

using AgentKit.Providers.Http;

/// <summary>Verifies GoogleVertexAIProviderDefaults behavior and contracts.</summary>
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
        var uri = GoogleVertexAIProviderDefaults.BuildGenerateContentUri(options, new ModelId("gemini-2.5-flash"), deploymentId: null, useStreaming: false);
        uri.ShouldBe(new Uri("https://us-central1-aiplatform.googleapis.com/v1/projects/my-project/locations/us-central1/" + "publishers/google/models/gemini-2.5-flash:generateContent"));
    }

    [Fact]
    public void BuildGenerateContentUri_WhenStreaming_AppendsAltSseQuery()
    {
        var options = CreateOptions();
        var uri = GoogleVertexAIProviderDefaults.BuildGenerateContentUri(options, new ModelId("gemini-2.5-flash"), deploymentId: null, useStreaming: true);
        uri.ShouldBe(new Uri("https://us-central1-aiplatform.googleapis.com/v1/projects/my-project/locations/us-central1/" + "publishers/google/models/gemini-2.5-flash:streamGenerateContent?alt=sse"));
    }

    [Fact]
    public void BuildGenerateContentUri_WhenDeploymentIdSupplied_UsesEndpointResourceInstead()
    {
        var options = CreateOptions();
        var uri = GoogleVertexAIProviderDefaults.BuildGenerateContentUri(options, new ModelId("gemini-2.5-flash"), new DeploymentId("my-endpoint"), useStreaming: false);
        uri.ShouldBe(new Uri("https://us-central1-aiplatform.googleapis.com/v1/projects/my-project/locations/us-central1/" + "endpoints/my-endpoint:generateContent"));
    }

    [Fact]
    public void BuildGenerateContentUri_WhenCustomPublisherAndApiVersion_UsesConfiguredValues()
    {
        var options = CreateOptions();
        options.Publisher = "anthropic";
        options.ApiVersion = "v1beta1";
        var uri = GoogleVertexAIProviderDefaults.BuildGenerateContentUri(options, new ModelId("claude-opus-4"), deploymentId: null, useStreaming: false);
        uri.ShouldBe(new Uri("https://us-central1-aiplatform.googleapis.com/v1beta1/projects/my-project/locations/us-central1/" + "publishers/anthropic/models/claude-opus-4:generateContent"));
    }

    [Fact]
    public void BuildPredictUri_WhenNoDeployment_UsesPublisherModelResource()
    {
        var options = CreateOptions();
        var uri = GoogleVertexAIProviderDefaults.BuildPredictUri(options, new ModelId("text-embedding-005"), deploymentId: null);
        uri.ShouldBe(new Uri("https://us-central1-aiplatform.googleapis.com/v1/projects/my-project/locations/us-central1/" + "publishers/google/models/text-embedding-005:predict"));
    }

    [Fact]
    public void BuildPredictUri_WhenDeploymentIdSupplied_UsesEndpointResourceInstead()
    {
        var options = CreateOptions();
        var uri = GoogleVertexAIProviderDefaults.BuildPredictUri(options, new ModelId("text-embedding-005"), new DeploymentId("my-endpoint"));
        uri.ShouldBe(new Uri("https://us-central1-aiplatform.googleapis.com/v1/projects/my-project/locations/us-central1/" + "endpoints/my-endpoint:predict"));
    }

    /// <summary>Verifies the global location resolves to Google's documented global host, not a nonexistent "global-aiplatform" host.</summary>
    [Fact]
    public void BuildGenerateContentUri_WhenLocationIsGlobal_UsesGlobalHostWithGlobalLocationInResourcePath()
    {
        var options = CreateOptions();
        options.Location = GoogleVertexAIProviderDefaults.GlobalLocation;
        var uri = GoogleVertexAIProviderDefaults.BuildGenerateContentUri(options, new ModelId("gemini-3-pro-preview"), deploymentId: null, useStreaming: false);
        uri.ShouldBe(new Uri("https://aiplatform.googleapis.com/v1/projects/my-project/locations/global/" + "publishers/google/models/gemini-3-pro-preview:generateContent"));
        uri.Host.ShouldBe("aiplatform.googleapis.com");
    }

    [Fact]
    public void BuildGenerateContentUri_WhenLocationIsGlobalAndStreaming_UsesGlobalHostAndAltSseQuery()
    {
        var options = CreateOptions();
        options.Location = "global";
        var uri = GoogleVertexAIProviderDefaults.BuildGenerateContentUri(options, new ModelId("gemini-3-pro-preview"), deploymentId: null, useStreaming: true);
        uri.ShouldBe(new Uri("https://aiplatform.googleapis.com/v1/projects/my-project/locations/global/" + "publishers/google/models/gemini-3-pro-preview:streamGenerateContent?alt=sse"));
    }

    [Fact]
    public void BuildPredictUri_WhenLocationIsGlobal_UsesGlobalHost()
    {
        var options = CreateOptions();
        options.Location = "global";
        var uri = GoogleVertexAIProviderDefaults.BuildPredictUri(options, new ModelId("gemini-embedding-001"), deploymentId: null);
        uri.ShouldBe(new Uri("https://aiplatform.googleapis.com/v1/projects/my-project/locations/global/" + "publishers/google/models/gemini-embedding-001:predict"));
    }

    /// <summary>Verifies an explicit base address replaces the host while the location still names the resource.</summary>
    [Fact]
    public void BuildGenerateContentUri_WhenBaseAddressConfigured_UsesItInsteadOfLocationDerivedHost()
    {
        var options = CreateOptions();
        options.BaseAddress = new Uri("https://vertex.psc.internal.example:8443/");
        var uri = GoogleVertexAIProviderDefaults.BuildGenerateContentUri(options, new ModelId("gemini-2.5-flash"), deploymentId: null, useStreaming: false);
        uri.ShouldBe(new Uri("https://vertex.psc.internal.example:8443/v1/projects/my-project/locations/us-central1/" + "publishers/google/models/gemini-2.5-flash:generateContent"));
    }

    [Fact]
    public void BuildPredictUri_WhenBaseAddressConfigured_UsesItInsteadOfLocationDerivedHost()
    {
        var options = CreateOptions();
        options.BaseAddress = new Uri("http://127.0.0.1:5005/");
        var uri = GoogleVertexAIProviderDefaults.BuildPredictUri(options, new ModelId("text-embedding-005"), deploymentId: null);
        uri.ShouldBe(new Uri("http://127.0.0.1:5005/v1/projects/my-project/locations/us-central1/" + "publishers/google/models/text-embedding-005:predict"));
    }

    [Fact]
    public void BuildGenerateContentUri_WhenBaseAddressConfiguredWithGlobalLocation_UsesExplicitBaseAddress()
    {
        var options = CreateOptions();
        options.Location = "global";
        options.BaseAddress = new Uri("https://aiplatform.us.rep.googleapis.com/");
        var uri = GoogleVertexAIProviderDefaults.BuildGenerateContentUri(options, new ModelId("gemini-2.5-flash"), deploymentId: null, useStreaming: false);
        uri.ShouldBe(new Uri("https://aiplatform.us.rep.googleapis.com/v1/projects/my-project/locations/global/" + "publishers/google/models/gemini-2.5-flash:generateContent"));
    }

    [Fact]
    public void BuildGenerateContentUri_WhenBaseAddressIsRelative_ThrowsArgumentException()
    {
        var options = CreateOptions();
        options.BaseAddress = new Uri("relative/path", UriKind.Relative);
        var exception = Should.Throw<ArgumentException>(() => GoogleVertexAIProviderDefaults.BuildGenerateContentUri(options, new ModelId("gemini-2.5-flash"), deploymentId: null, useStreaming: false));
        exception.ParamName.ShouldBe("options.BaseAddress");
    }

    [Fact]
    public void GlobalBaseAddress_IsDocumentedGlobalEndpointHost() =>
        GoogleVertexAIProviderDefaults.GlobalBaseAddress.ShouldBe(new Uri("https://aiplatform.googleapis.com/"));

    [Fact]
    public void GlobalLocation_IsGlobal() => GoogleVertexAIProviderDefaults.GlobalLocation.ShouldBe("global");

    [Theory]
    [InlineData("global")]
    [InlineData("GLOBAL")]
    [InlineData("Global")]
    public void BuildDefaultBaseAddress_WhenLocationIsGlobalInAnyCase_ReturnsGlobalBaseAddress(string location) =>
        GoogleVertexAIProviderDefaults.BuildDefaultBaseAddress(location).ShouldBe(GoogleVertexAIProviderDefaults.GlobalBaseAddress);

    [Theory]
    [InlineData("us-central1", "https://us-central1-aiplatform.googleapis.com/")]
    [InlineData("europe-west4", "https://europe-west4-aiplatform.googleapis.com/")]
    [InlineData("us", "https://us-aiplatform.googleapis.com/")]
    public void BuildDefaultBaseAddress_WhenLocationIsRegional_ReturnsRegionalHost(string location, string expected) =>
        GoogleVertexAIProviderDefaults.BuildDefaultBaseAddress(location).ShouldBe(new Uri(expected));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void BuildDefaultBaseAddress_WhenLocationIsNullOrWhiteSpace_ThrowsArgumentException(string? location)
    {
        var exception = Should.Throw<ArgumentException>(() => GoogleVertexAIProviderDefaults.BuildDefaultBaseAddress(location!));
        exception.ParamName.ShouldBe("location");
    }

    [Fact]
    public void BuildRegionalBaseAddress_WhenLocationIsRegion_ReturnsRegionalHost() =>
        GoogleVertexAIProviderDefaults.BuildRegionalBaseAddress("us-central1").ShouldBe(new Uri("https://us-central1-aiplatform.googleapis.com/"));

    [Fact]
    public void BuildRegionalBaseAddress_WhenLocationIsWhiteSpace_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => GoogleVertexAIProviderDefaults.BuildRegionalBaseAddress(" "));
        exception.ParamName.ShouldBe("location");
    }

    [Fact]
    public void ResolveBaseAddress_WhenBaseAddressUnsetAndLocationRegional_DerivesRegionalHost() =>
        GoogleVertexAIProviderDefaults.ResolveBaseAddress(CreateOptions()).ShouldBe(new Uri("https://us-central1-aiplatform.googleapis.com/"));

    [Fact]
    public void ResolveBaseAddress_WhenBaseAddressUnsetAndLocationGlobal_DerivesGlobalHost()
    {
        var options = CreateOptions();
        options.Location = "global";
        GoogleVertexAIProviderDefaults.ResolveBaseAddress(options).ShouldBe(GoogleVertexAIProviderDefaults.GlobalBaseAddress);
    }

    [Fact]
    public void ResolveBaseAddress_WhenBaseAddressSet_ReturnsItUnchanged()
    {
        var options = CreateOptions();
        options.BaseAddress = new Uri("https://proxy.example/");
        GoogleVertexAIProviderDefaults.ResolveBaseAddress(options).ShouldBeSameAs(options.BaseAddress);
    }

    [Fact]
    public void ResolveBaseAddress_WhenOptionsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => GoogleVertexAIProviderDefaults.ResolveBaseAddress(null!));
        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void ResolveBaseAddress_WhenBaseAddressUnsetAndLocationMissing_ThrowsArgumentException()
    {
        var options = new GoogleVertexAIProviderOptions { ProjectId = "my-project" };
        var exception = Should.Throw<ArgumentException>(() => GoogleVertexAIProviderDefaults.ResolveBaseAddress(options));
        exception.ParamName.ShouldBe("options.Location");
    }

    [Fact]
    public void ResolveBaseAddress_WhenBaseAddressIsRelative_ThrowsArgumentException()
    {
        var options = CreateOptions();
        options.BaseAddress = new Uri("relative", UriKind.Relative);
        var exception = Should.Throw<ArgumentException>(() => GoogleVertexAIProviderDefaults.ResolveBaseAddress(options));
        exception.ParamName.ShouldBe("options.BaseAddress");
    }

    [Fact]
    public void EmbeddingApiFamily_IsStableGoogleVertexAIPredictEmbeddingIdentity() => GoogleVertexAIProviderDefaults.EmbeddingApiFamily.ShouldBe(new ApiFamilyId("google-vertex-ai-predict-embedding"));
    [Fact]
    public void DefaultCapabilities_MatchesGeminiDefaults() => GoogleVertexAIProviderDefaults.DefaultCapabilities.ShouldBe(GoogleGeminiProviderDefaults.DefaultCapabilities);
    [Fact]
    public void DefaultEmbeddingCapabilities_SupportsPurposeAndDimensionsButNotEncodingSelection()
    {
        GoogleVertexAIProviderDefaults.DefaultEmbeddingCapabilities.SupportsPurpose.ShouldBeTrue();
        GoogleVertexAIProviderDefaults.DefaultEmbeddingCapabilities.SupportsDimensions.ShouldBeTrue();
        GoogleVertexAIProviderDefaults.DefaultEmbeddingCapabilities.SupportsEncodingSelection.ShouldBeFalse();
    }

    [Fact]
    public void AuthorizationScheme_WhenApiKeyCredential_IsDeniedBecauseVertexHasNoApiKeyMode()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero));

        GoogleVertexAIProviderDefaults.AuthorizationScheme.SupportsApiKey.ShouldBeFalse();
        var result = ProviderAuthorizationHeaderFactory.Create(
            new ApiKeyProviderCredential("some-key"),
            GoogleVertexAIProviderDefaults.ProviderId,
            clock,
            GoogleVertexAIProviderDefaults.AuthorizationScheme);

        var denied = result.ShouldBeOfType<ProviderAuthorizationDenied>();
        denied.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        denied.Failure.ProviderId.ShouldBe(GoogleVertexAIProviderDefaults.ProviderId);
    }
}
