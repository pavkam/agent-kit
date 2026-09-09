// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures immutable result-normalization rules selected before invocation.</summary>
/// <remarks>The normalizer, rather than this value, enforces aggregate canonical-byte limits. A resolved call carries an execution-policy reference; a pre-resolution rejection does not.</remarks>
public sealed record ToolResultNormalizationSnapshot
{
    /// <summary>Captures the exact policies, algorithm, and finite bounds selected before result bytes exist.</summary>
    /// <param name="rejectionPolicy">The run-level rejection policy retained even when resolution fails.</param>
    /// <param name="projectionPolicy">The exact later history-projection policy.</param>
    /// <param name="executionPolicy">The selected per-tool policy, or null for a pre-resolution rejection.</param>
    /// <param name="algorithmVersion">The positive deterministic normalizer revision.</param>
    /// <param name="bounds">The positive aggregate canonical-byte and part limits.</param>
    /// <param name="allowedTransformations">The closed set of transformations policy permits.</param>
    /// <param name="extensions">Compatible immutable evidence owned by the snapshot.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rejectionPolicy"/>, <paramref name="projectionPolicy"/>, <paramref name="bounds"/>, or <paramref name="extensions"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="algorithmVersion"/> is default or <paramref name="allowedTransformations"/> contains unknown bits.</exception>
    public ToolResultNormalizationSnapshot(
        ToolResultRejectionPolicyReference rejectionPolicy,
        ToolResultProjectionPolicyReference projectionPolicy,
        ToolExecutionPolicyReference? executionPolicy,
        ToolResultNormalizationAlgorithmVersion algorithmVersion,
        ToolResultBounds bounds,
        ToolResultProjectionTransformations allowedTransformations,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(rejectionPolicy);
        ArgumentNullException.ThrowIfNull(projectionPolicy);
        ArgumentOutOfRangeException.ThrowIfEqual(algorithmVersion, default);
        ArgumentNullException.ThrowIfNull(bounds);
        var unknownTransformations = allowedTransformations & ~(
            ToolResultProjectionTransformations.Redaction
            | ToolResultProjectionTransformations.Normalization
            | ToolResultProjectionTransformations.Summarization
            | ToolResultProjectionTransformations.Truncation
            | ToolResultProjectionTransformations.Externalization);
        ArgumentOutOfRangeException.ThrowIfNotEqual(
            unknownTransformations,
            ToolResultProjectionTransformations.None,
            nameof(allowedTransformations));
        ArgumentNullException.ThrowIfNull(extensions);

        RejectionPolicy = rejectionPolicy;
        ProjectionPolicy = projectionPolicy;
        ExecutionPolicy = executionPolicy;
        AlgorithmVersion = algorithmVersion;
        Bounds = bounds;
        AllowedTransformations = allowedTransformations;
        Extensions = extensions;
    }
    /// <summary>Gets the run-level rejection policy.</summary><value>A nonnull captured reference.</value>
    public ToolResultRejectionPolicyReference RejectionPolicy { get; }
    /// <summary>Gets the projection policy.</summary><value>A nonnull captured reference.</value>
    public ToolResultProjectionPolicyReference ProjectionPolicy { get; }
    /// <summary>Gets the selected per-tool policy.</summary><value>Null for pre-resolution rejection paths.</value>
    public ToolExecutionPolicyReference? ExecutionPolicy { get; }
    /// <summary>Gets the normalizer algorithm revision.</summary><value>A positive revision.</value>
    public ToolResultNormalizationAlgorithmVersion AlgorithmVersion { get; }
    /// <summary>Gets terminal-result bounds.</summary><value>A nonnull finite limit value.</value>
    public ToolResultBounds Bounds { get; }
    /// <summary>Gets permitted transformations.</summary><value>Only defined flags.</value>
    public ToolResultProjectionTransformations AllowedTransformations { get; }
    /// <summary>Gets compatible evidence.</summary><value>A nonnull immutable bag.</value>
    public ExtensionData Extensions { get; }
}
