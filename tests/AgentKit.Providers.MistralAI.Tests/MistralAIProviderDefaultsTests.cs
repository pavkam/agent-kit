// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Tests;

/// <summary>
/// Verifies <see cref="MistralAIProviderDefaults.BuildChatCompletionsUri"/>
/// and the shared provider identity/capability defaults.
/// </summary>
public sealed class MistralAIProviderDefaultsTests
{
    [Fact]
    public void BuildChatCompletionsUri_WhenGivenOptions_CombinesBaseAddressAndPath()
    {
        var options = new MistralAIProviderOptions
        {
            BaseAddress = new Uri("https://example.test/"),
            ChatCompletionsPath = "v2/chat/completions",
        };

        MistralAIProviderDefaults.BuildChatCompletionsUri(options).ShouldBe(new Uri("https://example.test/v2/chat/completions"));
    }

    [Fact]
    public void ProviderId_IsStableMistralAIIdentity() =>
        MistralAIProviderDefaults.ProviderId.ShouldBe(new ProviderId("mistral-ai"));

    [Fact]
    public void DefaultCapabilities_DoesNotAdvertiseReasoningOrVisionOrStructuredOutput()
    {
        MistralAIProviderDefaults.DefaultCapabilities.SupportsReasoning.ShouldBeFalse();
        MistralAIProviderDefaults.DefaultCapabilities.SupportsVisionInput.ShouldBeFalse();
        MistralAIProviderDefaults.DefaultCapabilities.SupportsStructuredOutput.ShouldBeFalse();
        MistralAIProviderDefaults.DefaultCapabilities.SupportsParallelToolCalls.ShouldBeTrue();
        MistralAIProviderDefaults.DefaultCapabilities.SupportsToolCalls.ShouldBeTrue();
    }
}
