// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Externalizes oversized terminal tool-result content behind an authorized artifact reference.</summary>
/// <remarks>
/// <para>
/// A spill is optional and selected per tool executor: an executor without one truncates oversized content. The result normalizer
/// calls it only when the captured normalization snapshot permits <see cref="ToolResultProjectionTransformations.Externalization"/>
/// and only for content larger than the aggregate canonical-byte bound, so the stored bytes are the complete content the bound
/// would otherwise discard.
/// </para>
/// <para>
/// Implementations authorize and store through the artifact coordinator under the call's captured authorization, never append a
/// session record, and record no reference-commit intent: the executor returns the reference in the terminal result and the run's
/// committed tool-result message is the reference commitment. A refusal is a typed outcome, never an exception, so the normalizer
/// can fall back to truncation.
/// </para>
/// </remarks>
public interface IToolResultSpill
{
    /// <summary>Stores the complete content of one call's result as an immutable artifact.</summary>
    /// <param name="request">The call identity and complete content to store.</param>
    /// <param name="cancellationToken">Cancels before the artifact is published.</param>
    /// <returns>The committed reference, or a typed refusal that leaves no readable artifact behind.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<ToolResultSpillResult> SpillAsync(ToolResultSpillRequest request, CancellationToken cancellationToken = default);
}
