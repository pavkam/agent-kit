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
    public void AssistantReasoningReplay_WhenNotAssigned_DefaultsToOmit() =>
        CreateProfile("chat/completions").AssistantReasoningReplay.ShouldBe(OpenAIAssistantReasoningReplay.Omit);

    [Fact]
    public void AssistantReasoningReplay_WhenAssignedDefinedValue_RetainsIt()
    {
        // Arrange
        var profile = CreateProfile("chat/completions");

        // Act
        var replayed = profile with { AssistantReasoningReplay = OpenAIAssistantReasoningReplay.ReasoningContentField };

        // Assert
        replayed.AssistantReasoningReplay.ShouldBe(OpenAIAssistantReasoningReplay.ReasoningContentField);
        profile.AssistantReasoningReplay.ShouldBe(OpenAIAssistantReasoningReplay.Omit);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void AssistantReasoningReplay_WhenUndefined_RejectsBeforeAssignment(int value)
    {
        // Arrange
        var profile = CreateProfile("chat/completions");

        // Act / Assert
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            profile with { AssistantReasoningReplay = (OpenAIAssistantReasoningReplay) value });
        exception.ParamName.ShouldBe("value");
        profile.AssistantReasoningReplay.ShouldBe(OpenAIAssistantReasoningReplay.Omit);
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

    [Fact]
    public void ChatCompletionsUri_WhenBaseAddressHasNoTrailingSlash_StillIncludesTheFullBasePath()
    {
        // new Uri(BaseAddress, ChatCompletionsPath) uses RFC 3986 relative resolution: without a
        // trailing slash, "https://host/openai/v1" + "chat/completions" resolves to
        // "https://host/openai/chat/completions", silently dropping "v1" - extremely common when a
        // caller binds a base address from configuration without a trailing slash.
        var profile = new OpenAICompatibilityProfile(
            new Uri("https://api.example.test/openai/v1"),
            "chat/completions",
            sendDeveloperRoleAsSystem: false,
            preferStreaming: true,
            includeStreamUsage: true,
            useMaxCompletionTokensField: true,
            []);

        profile.ChatCompletionsUri.ShouldBe(new Uri("https://api.example.test/openai/v1/chat/completions"));
    }

    [Fact]
    public void EmbeddingsUri_WhenBaseAddressHasNoTrailingSlash_StillIncludesTheFullBasePath()
    {
        var profile = new OpenAICompatibilityProfile(
            new Uri("https://api.example.test/openai/v1"),
            "chat/completions",
            sendDeveloperRoleAsSystem: false,
            preferStreaming: true,
            includeStreamUsage: true,
            useMaxCompletionTokensField: true,
            [],
            embeddingsPath: "embeddings",
            supportsEmbeddingPurpose: false);

        profile.EmbeddingsUri.ShouldBe(new Uri("https://api.example.test/openai/v1/embeddings"));
    }

    [Fact]
    public void BaseAddress_WhenAlreadyEndsWithSlash_IsUnchanged()
    {
        var address = new Uri("https://api.example.test/openai/v1/");
        var profile = new OpenAICompatibilityProfile(
            address,
            "chat/completions",
            sendDeveloperRoleAsSystem: false,
            preferStreaming: true,
            includeStreamUsage: true,
            useMaxCompletionTokensField: true,
            []);

        profile.BaseAddress.ShouldBe(address);
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
