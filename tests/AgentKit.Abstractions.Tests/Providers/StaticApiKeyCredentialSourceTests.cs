// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies StaticApiKeyCredentialSource behavior and contracts.</summary>
public sealed class StaticApiKeyCredentialSourceTests
{
    [Fact]
    public void Constructor_WhenApiKeyIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new StaticApiKeyCredentialSource(" ")).ParamName.ShouldBe("apiKey");

    [Fact]
    public async Task GetCredentialAsync_WhenCalled_ReturnsStaticCredential()
    {
        var source = new StaticApiKeyCredentialSource("key");
        var credential = await source.GetCredentialAsync(new ProviderId("openai"), TestContext.Current.CancellationToken);
        credential.ShouldBe(new ApiKeyProviderCredential("key"));
    }
}
