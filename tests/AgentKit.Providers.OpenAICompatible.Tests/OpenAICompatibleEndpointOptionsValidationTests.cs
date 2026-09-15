// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Tests;

/// <summary>Verifies OpenAICompatibleEndpointOptionsValidation behavior and contracts.</summary>
public sealed class OpenAICompatibleEndpointOptionsValidationTests
{
    private static readonly Uri BaseAddress = new("https://api.example.test/v1/");

    [Fact]
    public void Validate_WhenChatOnlyOptionsAreValid_ReturnsNoFailures()
    {
        var failures = OpenAICompatibleEndpointOptionsValidation.Validate(BaseAddress, "chat/completions", "BaseAddress", "ChatCompletionsPath");

        failures.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenChatAndEmbeddingsOptionsAreValid_ReturnsNoFailures()
    {
        var failures = OpenAICompatibleEndpointOptionsValidation.Validate(BaseAddress, "chat/completions", "embeddings", "BaseAddress", "ChatCompletionsPath", "EmbeddingsPath");

        failures.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenBaseAddressIsNull_ReportsBaseAddressMember()
    {
        var failures = OpenAICompatibleEndpointOptionsValidation.Validate(null, "chat/completions", "ResourceEndpoint", "ChatCompletionsPath");

        failures.ShouldBe(["ResourceEndpoint must be an absolute URI."]);
    }

    [Fact]
    public void Validate_WhenBaseAddressIsRelative_ReportsBaseAddressMember()
    {
        var failures = OpenAICompatibleEndpointOptionsValidation.Validate(new Uri("not-absolute", UriKind.Relative), "chat/completions", "BaseAddress", "ChatCompletionsPath");

        failures.ShouldBe(["BaseAddress must be an absolute URI."]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/chat/completions")]
    [InlineData("\\chat\\completions")]
    [InlineData("//evil.example.test/chat")]
    [InlineData("https://evil.example.test/chat")]
    public void Validate_WhenChatPathCannotBeSafelyResolved_ReportsChatPathMember(string? path)
    {
        var failures = OpenAICompatibleEndpointOptionsValidation.Validate(BaseAddress, path, "BaseAddress", "ChatCompletionsPath");

        failures.ShouldBe(["ChatCompletionsPath must be a non-rooted relative URI path."]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/embeddings")]
    [InlineData("//evil.example.test/embeddings")]
    [InlineData("https://evil.example.test/embeddings")]
    public void Validate_WhenEmbeddingsPathCannotBeSafelyResolved_ReportsEmbeddingsPathMember(string? path)
    {
        var failures = OpenAICompatibleEndpointOptionsValidation.Validate(BaseAddress, "chat/completions", path, "BaseAddress", "ChatCompletionsPath", "EmbeddingsPath");

        failures.ShouldBe(["EmbeddingsPath must be a non-rooted relative URI path."]);
    }

    [Fact]
    public void Validate_WhenEveryMemberIsInvalid_ReportsAllFailuresInMemberOrder()
    {
        var failures = OpenAICompatibleEndpointOptionsValidation.Validate(null, "/chat", "https://evil.example.test/embed", "BaseAddress", "ChatCompletionsPath", "EmbeddingsPath");

        failures.ShouldBe(
        [
            "BaseAddress must be an absolute URI.",
            "ChatCompletionsPath must be a non-rooted relative URI path.",
            "EmbeddingsPath must be a non-rooted relative URI path.",
        ]);
    }

    [Theory]
    [InlineData("chat/completions", "embeddings")]
    [InlineData("v1/chat/completions", "v1/embeddings")]
    [InlineData("openai/v1/chat/completions", "openai/v1/embeddings")]
    [InlineData("chat/completions?api-version=2024-10-21", "embeddings?api-version=2024-10-21")]
    public void Validate_WhenPathsPass_ProfileConstructionAcceptsTheSameValues(string chatPath, string embeddingsPath)
    {
        var failures = OpenAICompatibleEndpointOptionsValidation.Validate(BaseAddress, chatPath, embeddingsPath, "BaseAddress", "ChatCompletionsPath", "EmbeddingsPath");

        failures.ShouldBeEmpty();
        _ = Should.NotThrow(() => new OpenAICompatibilityProfile(BaseAddress, chatPath, false, false, false, false, [], embeddingsPath));
    }

    [Theory]
    [InlineData("/chat/completions", "embeddings")]
    [InlineData("chat/completions", "//evil.example.test/embeddings")]
    [InlineData(" ", "embeddings")]
    public void Validate_WhenPathsFail_ProfileConstructionRejectsTheSameValues(string chatPath, string embeddingsPath)
    {
        var failures = OpenAICompatibleEndpointOptionsValidation.Validate(BaseAddress, chatPath, embeddingsPath, "BaseAddress", "ChatCompletionsPath", "EmbeddingsPath");

        failures.ShouldNotBeEmpty();
        _ = Should.Throw<ArgumentException>(() => new OpenAICompatibilityProfile(BaseAddress, chatPath, false, false, false, false, [], embeddingsPath));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_WhenBaseAddressNameIsMissing_ThrowsArgumentException(string? memberName)
    {
        var exception = Should.Throw<ArgumentException>(() => OpenAICompatibleEndpointOptionsValidation.Validate(BaseAddress, "chat/completions", memberName!, "ChatCompletionsPath"));

        exception.ParamName.ShouldBe("baseAddressName");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_WhenChatCompletionsPathNameIsMissing_ThrowsArgumentException(string? memberName)
    {
        var exception = Should.Throw<ArgumentException>(() => OpenAICompatibleEndpointOptionsValidation.Validate(BaseAddress, "chat/completions", "BaseAddress", memberName!));

        exception.ParamName.ShouldBe("chatCompletionsPathName");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_WhenEmbeddingsPathNameIsMissing_ThrowsArgumentException(string? memberName)
    {
        var exception = Should.Throw<ArgumentException>(() => OpenAICompatibleEndpointOptionsValidation.Validate(BaseAddress, "chat/completions", "embeddings", "BaseAddress", "ChatCompletionsPath", memberName!));

        exception.ParamName.ShouldBe("embeddingsPathName");
    }
}
