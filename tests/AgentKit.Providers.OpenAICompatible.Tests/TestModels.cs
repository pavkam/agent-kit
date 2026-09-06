// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Tests;

/// <summary>Shared <see cref="ModelDescriptor"/> fixtures reused across tests.</summary>
internal static class TestModels
{
    /// <summary>Gets a representative descriptor for OpenAI's <c>gpt-4o</c> model.</summary>
    public static ModelDescriptor Gpt4O { get; } = new(
        new ModelAlias("chat"),
        new ProviderId("openai"),
        new ApiFamilyId("openai-chat-completions"),
        new ModelId("gpt-4o"),
        deploymentId: null,
        new ModelCapabilities(
            supportsSystemInstructions: true,
            supportsStreaming: true,
            supportsToolCalls: true,
            supportsParallelToolCalls: true,
            supportsStructuredOutput: true,
            supportsReasoning: false,
            supportsVisionInput: false,
            ExtensionData.Empty),
        new ModelLimits(maxContextTokens: null, maxOutputTokens: null),
        pricing: null,
        ExtensionData.Empty);

    /// <summary>Gets a representative descriptor for a model that does not support tool calls.</summary>
    public static ModelDescriptor NoToolSupport { get; } = Gpt4O with
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
