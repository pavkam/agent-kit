// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Tests;

/// <summary>Shared <see cref="ModelDescriptor"/> fixtures reused across tests.</summary>
internal static class TestModels
{
    /// <summary>Gets a representative descriptor for Gemini's <c>gemini-2.5-flash</c> model.</summary>
    public static ModelDescriptor GeminiFlash { get; } = new(
        new ModelAlias("chat"),
        GoogleGeminiProviderDefaults.ProviderId,
        GoogleGeminiProviderDefaults.ApiFamily,
        new ModelId("gemini-2.5-flash"),
        deploymentId: null,
        GoogleGeminiProviderDefaults.DefaultCapabilities,
        GoogleGeminiProviderDefaults.DefaultLimits,
        pricing: null,
        ExtensionData.Empty);

    /// <summary>Gets a representative descriptor for a model that does not support tool calls.</summary>
    public static ModelDescriptor NoToolSupport { get; } = GeminiFlash with
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
