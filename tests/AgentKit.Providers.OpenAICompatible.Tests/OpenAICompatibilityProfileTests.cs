// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Tests;

/// <summary>
/// Verifies that compatibility profiles retain their validated endpoint and
/// wire-behavior invariants for their entire lifetime.
/// </summary>
public sealed class OpenAICompatibilityProfileTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MaximumToolResultCharacters_WhenNonPositive_RejectsBeforeAssignment(int value)
    {
        // Arrange
        var profile = new OpenAICompatibilityProfile(new Uri("https://api.example.test/"),
            "chat/completions", false, false, true, true, []);

        // Act / Assert
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            profile with { MaximumToolResultCharacters = value });
        exception.ParamName.ShouldBe("value");
        profile.MaximumToolResultCharacters.ShouldBe(262_144);
    }

    [Fact]
    public void Constructor_WhenExistingEightParameterSignatureIsInspected_RemainsAvailableWithItsDefault()
    {
        var constructor = typeof(OpenAICompatibilityProfile).GetConstructor([
            typeof(Uri),
            typeof(string),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(ImmutableDictionary<string, string>),
            typeof(string),
        ]).ShouldNotBeNull();

        var parameters = constructor.GetParameters();
        parameters.Length.ShouldBe(8);
        parameters.Single(parameter => parameter.Name == "embeddingsPath").HasDefaultValue.ShouldBeTrue();
        parameters[^1].DefaultValue.ShouldBeNull();

        var profile = new OpenAICompatibilityProfile(
            new Uri("https://api.example.test/v1/"),
            "chat/completions",
            sendDeveloperRoleAsSystem: false,
            preferStreaming: true,
            includeStreamUsage: true,
            useMaxCompletionTokensField: true,
            []);

        profile.SupportsEmbeddingPurpose.ShouldBeFalse();
        profile.EmbeddingsPath.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenNewNineParameterSignatureIsInspected_RequiresPurposeCapabilityDeclaration()
    {
        var constructor = typeof(OpenAICompatibilityProfile).GetConstructor([
            typeof(Uri),
            typeof(string),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(ImmutableDictionary<string, string>),
            typeof(string),
            typeof(bool),
        ]).ShouldNotBeNull();

        var parameters = constructor.GetParameters();
        parameters.Length.ShouldBe(9);
        parameters[7].HasDefaultValue.ShouldBeFalse();
        parameters[^1].HasDefaultValue.ShouldBeFalse();

        var profile = new OpenAICompatibilityProfile(
            new Uri("https://api.example.test/v1/"),
            "chat/completions",
            sendDeveloperRoleAsSystem: false,
            preferStreaming: true,
            includeStreamUsage: true,
            useMaxCompletionTokensField: true,
            [],
            embeddingsPath: "embeddings",
            supportsEmbeddingPurpose: true);

        profile.SupportsEmbeddingPurpose.ShouldBeTrue();
        profile.EmbeddingsPath.ShouldBe("embeddings");
    }

    [Fact]
    public void Constructor_WhenChatCompletionsPathIsAbsolute_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => CreateProfile("https://evil.example.test/chat"));

        exception.ParamName.ShouldBe("chatCompletionsPath");
    }

    [Theory]
    [InlineData("/rooted/chat")]
    [InlineData("//evil.example.test/chat")]
    [InlineData("\\evil.example.test\\chat")]
    public void Constructor_WhenChatCompletionsPathIsRooted_ThrowsArgumentException(string path)
    {
        var exception = Should.Throw<ArgumentException>(() => CreateProfile(path));

        exception.ParamName.ShouldBe("chatCompletionsPath");
    }

    [Fact]
    public void Constructor_WhenBaseAddressIsRelative_ThrowsArgumentException()
    {
        var baseAddress = new Uri("v1/", UriKind.Relative);

        var exception = Should.Throw<ArgumentException>(() => new OpenAICompatibilityProfile(
            baseAddress,
            "chat/completions",
            sendDeveloperRoleAsSystem: false,
            preferStreaming: true,
            includeStreamUsage: true,
            useMaxCompletionTokensField: true,
            []));

        exception.ParamName.ShouldBe("baseAddress");
    }

    [Theory]
    [InlineData(nameof(OpenAICompatibilityProfile.BaseAddress))]
    [InlineData(nameof(OpenAICompatibilityProfile.ChatCompletionsPath))]
    [InlineData(nameof(OpenAICompatibilityProfile.SendDeveloperRoleAsSystem))]
    [InlineData(nameof(OpenAICompatibilityProfile.PreferStreaming))]
    [InlineData(nameof(OpenAICompatibilityProfile.IncludeStreamUsage))]
    [InlineData(nameof(OpenAICompatibilityProfile.UseMaxCompletionTokensField))]
    [InlineData(nameof(OpenAICompatibilityProfile.DefaultRequestHeaders))]
    [InlineData(nameof(OpenAICompatibilityProfile.EmbeddingsPath))]
    [InlineData(nameof(OpenAICompatibilityProfile.SupportsEmbeddingPurpose))]
    public void Properties_WhenInspected_AreConstructionOnly(string propertyName)
    {
        var property = typeof(OpenAICompatibilityProfile).GetProperty(propertyName).ShouldNotBeNull();

        property.SetMethod.ShouldBeNull();
    }

    [Fact]
    public void EmbeddingsUri_WhenEmbeddingsPathNotConfigured_IsNull() =>
        CreateProfile("chat/completions").EmbeddingsUri.ShouldBeNull();

    [Fact]
    public void EmbeddingsUri_WhenEmbeddingsPathConfigured_CombinesWithBaseAddress()
    {
        var profile = new OpenAICompatibilityProfile(
            new Uri("https://api.example.test/v1/"),
            "chat/completions",
            sendDeveloperRoleAsSystem: false,
            preferStreaming: true,
            includeStreamUsage: true,
            useMaxCompletionTokensField: true,
            [],
            embeddingsPath: "embeddings");

        profile.EmbeddingsUri.ShouldBe(new Uri("https://api.example.test/v1/embeddings"));
    }

    [Fact]
    public void Constructor_WhenEmbeddingsPathIsAbsolute_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new OpenAICompatibilityProfile(
            new Uri("https://api.example.test/v1/"),
            "chat/completions",
            sendDeveloperRoleAsSystem: false,
            preferStreaming: true,
            includeStreamUsage: true,
            useMaxCompletionTokensField: true,
            [],
            embeddingsPath: "https://evil.example.test/embeddings"));

        exception.ParamName.ShouldBe("embeddingsPath");
    }

    private static OpenAICompatibilityProfile CreateProfile(string chatCompletionsPath) =>
        new(
            new Uri("https://api.example.test/v1/"),
            chatCompletionsPath,
            sendDeveloperRoleAsSystem: false,
            preferStreaming: true,
            includeStreamUsage: true,
            useMaxCompletionTokensField: true,
            []);
}
