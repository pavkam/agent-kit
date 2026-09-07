// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic.Tests;

/// <summary>Shared <see cref="ModelDescriptor"/> fixtures reused across tests.</summary>
internal static class TestModels
{
    /// <summary>Gets a representative descriptor for Anthropic's <c>claude-sonnet-4-5</c> model.</summary>
    public static ModelDescriptor ClaudeSonnet { get; } = new(
        new ModelAlias("chat"),
        AnthropicProviderDefaults.ProviderId,
        AnthropicProviderDefaults.ApiFamily,
        new ModelId("claude-sonnet-4-5"),
        deploymentId: null,
        AnthropicProviderDefaults.DefaultCapabilities,
        AnthropicProviderDefaults.DefaultLimits,
        pricing: null,
        ExtensionData.Empty);

    /// <summary>Gets a representative descriptor for a model that does not support tool calls.</summary>
    public static ModelDescriptor NoToolSupport { get; } = ClaudeSonnet with
    {
        Capabilities = new ModelCapabilities(
            supportsSystemInstructions: true,
            supportsStreaming: true,
            supportsToolCalls: false,
            supportsParallelToolCalls: false,
            supportsStructuredOutput: false,
            supportsReasoning: false,
            supportsVisionInput: false,
            ExtensionData.Empty),
    };
}
