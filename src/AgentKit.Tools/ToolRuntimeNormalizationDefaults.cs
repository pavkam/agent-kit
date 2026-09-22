// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Captures immutable default normalization and projection evidence for the first-party tool executor.</summary>
internal static class ToolRuntimeNormalizationDefaults
{
    private static readonly ToolResultRejectionPolicyReference _rejectionPolicy = new(
        new ToolResultRejectionPolicyKey("agentkit.tools.default-rejection"),
        new ToolResultRejectionPolicyVersion(1));

    private static readonly ToolResultNormalizationAlgorithmVersion _algorithmVersion = new(1);

    /// <summary>Gets the default pre-policy rejection normalization snapshot.</summary>
    internal static ToolResultNormalizationSnapshot RejectionSnapshot { get; } = new(
        _rejectionPolicy,
        ToolResultProjectionPolicyReference.Default,
        executionPolicy: null,
        _algorithmVersion,
        new ToolResultBounds(262_144, 64),
        ToolResultProjectionTransformations.None,
        ExtensionData.Empty);

    /// <summary>Builds the normalization snapshot captured for one resolved descriptor.</summary>
    /// <param name="executionPolicy">The selected execution-policy reference from the catalog snapshot.</param>
    /// <returns>The immutable normalization rules for accepted and terminal paths.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="executionPolicy"/> is null.</exception>
    internal static ToolResultNormalizationSnapshot ForResolvedTool(ToolExecutionPolicyReference executionPolicy)
    {
        ArgumentNullException.ThrowIfNull(executionPolicy);
        return new ToolResultNormalizationSnapshot(
            _rejectionPolicy,
            ToolResultProjectionPolicyReference.Default,
            executionPolicy,
            _algorithmVersion,
            new ToolResultBounds(4_194_304, 64),
            ToolResultProjectionTransformations.None,
            ExtensionData.Empty);
    }
}
