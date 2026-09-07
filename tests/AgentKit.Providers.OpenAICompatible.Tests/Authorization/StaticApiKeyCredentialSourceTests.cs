// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Tests.Authorization;

/// <summary>
/// Verifies <see cref="StaticApiKeyCredentialSource"/>, the shared API-key
/// credential source reused by every OpenAI-compatible provider package.
/// </summary>
public sealed class StaticApiKeyCredentialSourceTests
{
    [Fact]
    public async Task GetCredentialAsync_WhenAsked_AlwaysResolvesToConfiguredApiKey()
    {
        var source = new StaticApiKeyCredentialSource("sk-configured");

        var first = await source.GetCredentialAsync(new ProviderId("openai"), TestContext.Current.CancellationToken);
        var second = await source.GetCredentialAsync(new ProviderId("z-ai"), TestContext.Current.CancellationToken);

        first.ShouldBeOfType<ApiKeyProviderCredential>().ApiKey.ShouldBe("sk-configured");
        second.ShouldBeOfType<ApiKeyProviderCredential>().ApiKey.ShouldBe("sk-configured");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenApiKeyIsNullOrWhitespace_ThrowsArgumentException(string apiKey) =>
        Should.Throw<ArgumentException>(() => new StaticApiKeyCredentialSource(apiKey));

    [Fact]
    public void Constructor_WhenApiKeyIsNull_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new StaticApiKeyCredentialSource(null!));
}
