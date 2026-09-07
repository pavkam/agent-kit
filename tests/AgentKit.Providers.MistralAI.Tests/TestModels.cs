// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Tests;

/// <summary>Shared <see cref="ModelDescriptor"/> fixtures reused across tests.</summary>
internal static class TestModels
{
    /// <summary>Gets a representative descriptor for Mistral's <c>mistral-large-latest</c> model.</summary>
    public static ModelDescriptor MistralLarge { get; } = new(
        new ModelAlias("chat"),
        MistralAIProviderDefaults.ProviderId,
        MistralAIProviderDefaults.ApiFamily,
        new ModelId("mistral-large-latest"),
        deploymentId: null,
        MistralAIProviderDefaults.DefaultCapabilities,
        MistralAIProviderDefaults.DefaultLimits,
        pricing: null,
        ExtensionData.Empty);

    /// <summary>Gets a representative descriptor for a model that does not support tool calls.</summary>
    public static ModelDescriptor NoToolSupport { get; } = MistralLarge with
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
