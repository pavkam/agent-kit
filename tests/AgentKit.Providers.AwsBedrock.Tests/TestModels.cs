// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Tests;

/// <summary>Shared <see cref="ModelDescriptor"/> fixtures reused across tests.</summary>
internal static class TestModels
{
    /// <summary>Gets a representative descriptor for a Claude 3 Sonnet model served through Bedrock.</summary>
    public static ModelDescriptor ClaudeSonnet { get; } = new(
        new ModelAlias("chat"),
        AwsBedrockProviderDefaults.ProviderId,
        AwsBedrockProviderDefaults.ApiFamily,
        new ModelId("anthropic.claude-3-sonnet-20240229-v1:0"),
        deploymentId: null,
        AwsBedrockProviderDefaults.DefaultCapabilities,
        AwsBedrockProviderDefaults.DefaultLimits,
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
