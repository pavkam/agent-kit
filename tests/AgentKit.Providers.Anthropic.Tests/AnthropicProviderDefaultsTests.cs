// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic.Tests;

using AgentKit.Providers.Http;

/// <summary>Verifies AnthropicProviderDefaults behavior and contracts.</summary>
public sealed class AnthropicProviderDefaultsTests
{
    [Fact]
    public void BuildMessagesUri_WhenGivenOptions_CombinesBaseAddressAndPath()
    {
        var options = new AnthropicProviderOptions
        {
            BaseAddress = new Uri("https://example.test/"),
            MessagesPath = "v2/messages",
        };
        AnthropicProviderDefaults.BuildMessagesUri(options).ShouldBe(new Uri("https://example.test/v2/messages"));
    }

    [Fact]
    public void DefaultCapabilities_SupportsReasoningButNotVisionOrStructuredOutput()
    {
        AnthropicProviderDefaults.DefaultCapabilities.SupportsReasoning.ShouldBeTrue();
        AnthropicProviderDefaults.DefaultCapabilities.SupportsVisionInput.ShouldBeFalse();
        AnthropicProviderDefaults.DefaultCapabilities.SupportsStructuredOutput.ShouldBeFalse();
    }

    [Fact]
    public void AuthorizationScheme_WhenApiKeyCredential_ResolvesToxapikeyHeader()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero));

        var result = ProviderAuthorizationHeaderFactory.Create(
            new ApiKeyProviderCredential("sk-ant-test"),
            AnthropicProviderDefaults.ProviderId,
            clock,
            AnthropicProviderDefaults.AuthorizationScheme);

        var granted = result.ShouldBeOfType<ProviderAuthorizationGranted>();
        granted.HeaderName.ShouldBe("x-api-key");
        granted.HeaderValue.ShouldBe("sk-ant-test");
    }
}
