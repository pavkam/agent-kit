// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Deterministically projects one authoritative terminal record into a bounded history/model value.</summary>
/// <remarks>
/// Projection applies the exact retained policy snapshot named by the terminal record. It never invokes the tool,
/// repeats an external effect, or selects a newer policy revision.
/// </remarks>
public interface IToolResultProjector
{
    /// <summary>Projects one terminal record under an exact retained policy snapshot.</summary>
    /// <param name="result">The authoritative terminal record to project.</param>
    /// <param name="policy">The immutable policy snapshot resolved for <paramref name="result"/>'s projection reference.</param>
    /// <param name="cancellationToken">Signals cancellation before projection completes.</param>
    /// <returns>The bounded projected tool result part.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="policy"/> is null.</exception>
    public ValueTask<ToolResultPart> ProjectAsync(
        ToolCallResult result,
        ToolResultProjectionPolicySnapshot policy,
        CancellationToken cancellationToken = default);
}
