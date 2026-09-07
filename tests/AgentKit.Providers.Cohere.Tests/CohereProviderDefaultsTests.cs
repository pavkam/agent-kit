// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Tests;

/// <summary>
/// Verifies <see cref="CohereProviderDefaults.BuildChatUri"/> and the
/// shared provider identity/capability defaults.
/// </summary>
public sealed class CohereProviderDefaultsTests
{
    [Fact]
    public void BuildChatUri_WhenGivenOptions_CombinesBaseAddressAndPath()
    {
        var options = new CohereProviderOptions
        {
            BaseAddress = new Uri("https://example.test/"),
            ChatPath = "v3/chat",
        };

        CohereProviderDefaults.BuildChatUri(options).ShouldBe(new Uri("https://example.test/v3/chat"));
    }

    [Fact]
    public void ProviderId_IsStableCohereIdentity() =>
        CohereProviderDefaults.ProviderId.ShouldBe(new ProviderId("cohere"));

    [Fact]
    public void DefaultCapabilities_SupportsReasoningButNotVisionOrStructuredOutput()
    {
        CohereProviderDefaults.DefaultCapabilities.SupportsReasoning.ShouldBeTrue();
        CohereProviderDefaults.DefaultCapabilities.SupportsVisionInput.ShouldBeFalse();
        CohereProviderDefaults.DefaultCapabilities.SupportsStructuredOutput.ShouldBeFalse();
        CohereProviderDefaults.DefaultCapabilities.SupportsParallelToolCalls.ShouldBeTrue();
        CohereProviderDefaults.DefaultCapabilities.SupportsToolCalls.ShouldBeTrue();
    }
}
