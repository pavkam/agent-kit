// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAI.Tests;

using AgentKit.Providers.OpenAI.Tests.Fakes;

/// <summary>
/// Verifies the two first-party <see cref="IProviderCredentialSource"/>
/// implementations: a static API key and an application-owned OAuth token
/// provider adapter.
/// </summary>
public sealed class OpenAICredentialSourceTests
{
    [Fact]
    public async Task OpenAIApiKeyCredentialSource_WhenAsked_AlwaysResolvesToConfiguredApiKey()
    {
        var source = new OpenAIApiKeyCredentialSource("sk-configured");

        var credential = await source.GetCredentialAsync(OpenAIProviderDefaults.ProviderId, TestContext.Current.CancellationToken);

        var apiKey = credential.ShouldBeOfType<ApiKeyProviderCredential>();
        apiKey.ApiKey.ShouldBe("sk-configured");
    }

    [Fact]
    public void OpenAIApiKeyCredentialSource_WhenApiKeyIsWhitespace_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new OpenAIApiKeyCredentialSource("   "));

    [Fact]
    public async Task OpenAIOAuthTokenCredentialSource_WhenAsked_DelegatesToTokenProvider()
    {
        var expectedCredential = new OAuthTokenProviderCredential("access-token", DateTimeOffset.UtcNow.AddHours(1));
        var source = new OpenAIOAuthTokenCredentialSource(new StaticOAuthTokenProvider(expectedCredential));

        var credential = await source.GetCredentialAsync(OpenAIProviderDefaults.ProviderId, TestContext.Current.CancellationToken);

        credential.ShouldBeSameAs(expectedCredential);
    }

    [Fact]
    public void OpenAIOAuthTokenCredentialSource_WhenTokenProviderIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new OpenAIOAuthTokenCredentialSource(null!));
}
