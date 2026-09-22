// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Normalizes one invocation's raw evidence under a captured immutable normalization snapshot.</summary>
/// <remarks>
/// The normalizer owns canonical retained-content encoding and aggregate byte enforcement for terminal recording.
/// It never invokes the tool, repeats an external effect, or selects a different policy revision than the snapshot
/// names.
/// </remarks>
public interface IToolResultNormalizer
{
    /// <summary>Normalizes raw invocation evidence under the captured snapshot.</summary>
    /// <param name="validatedCall">The validated call whose terminal record is being constructed.</param>
    /// <param name="invocation">The owned raw invocation evidence.</param>
    /// <param name="snapshot">The immutable normalization rules captured for this path.</param>
    /// <param name="cancellationToken">Signals cancellation before a closed outcome is returned.</param>
    /// <returns>The closed normalized or normalization-failed outcome.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="validatedCall"/>, <paramref name="invocation"/>, or <paramref name="snapshot"/> is null.
    /// </exception>
    public ValueTask<ToolResultNormalizationResult> NormalizeAsync(
        ValidatedToolCall validatedCall,
        ToolInvocationResult invocation,
        ToolResultNormalizationSnapshot snapshot,
        CancellationToken cancellationToken = default);
}
