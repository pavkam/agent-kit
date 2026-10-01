// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Builds minimal conversational <see cref="ModelDescriptor"/> values for composition tests.</summary>
public static class ModelDescriptorFixtures
{
    /// <summary>Builds a streaming, tool-capable chat descriptor.</summary>
    /// <param name="alias">The catalog alias text; defaults to <c>chat</c>.</param>
    /// <returns>An immutable descriptor under a fixed test provider.</returns>
    public static ModelDescriptor Chat(string alias = "chat") => new(
        new ModelAlias(alias),
        new ProviderId("test-provider"),
        new ApiFamilyId("test-api"),
        new ModelId("test-model"),
        deploymentId: null,
        new ModelCapabilities(
            supportsSystemInstructions: true,
            supportsStreaming: true,
            supportsToolCalls: true,
            supportsParallelToolCalls: true,
            supportsStructuredOutput: true,
            supportsReasoning: true,
            supportsVisionInput: true,
            ExtensionData.Empty),
        new ModelLimits(maxContextTokens: 4096, maxOutputTokens: 1024),
        pricing: null,
        ExtensionData.Empty);

    /// <summary>Builds a catalog snapshot that publishes the given conversational models.</summary>
    /// <param name="aliases">The aliases to publish; defaults to one <c>chat</c> model.</param>
    /// <returns>A version-one snapshot.</returns>
    public static ModelCatalogSnapshot Catalog(params string[] aliases) => new(
        new ModelCatalogVersion(1),
        [.. (aliases.Length == 0 ? ["chat"] : aliases).Select(Chat)]);
}
