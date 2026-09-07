// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Tests;

/// <summary>Shared <see cref="ModelDescriptor"/> fixtures reused across tests.</summary>
internal static class TestModels
{
    /// <summary>Gets a representative descriptor for Cohere's <c>command-a-plus-05-2026</c> model.</summary>
    public static ModelDescriptor CommandAPlus { get; } = new(
        new ModelAlias("chat"),
        CohereProviderDefaults.ProviderId,
        CohereProviderDefaults.ApiFamily,
        new ModelId("command-a-plus-05-2026"),
        deploymentId: null,
        CohereProviderDefaults.DefaultCapabilities,
        CohereProviderDefaults.DefaultLimits,
        pricing: null,
        ExtensionData.Empty);

    /// <summary>Gets a representative descriptor for a model that does not support tool calls.</summary>
    public static ModelDescriptor NoToolSupport { get; } = CommandAPlus with
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
