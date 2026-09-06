// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Chooses a structurally safe cut over a compaction source snapshot,
/// separately from producing the checkpoint content itself.
/// </summary>
/// <remarks>
/// A cut selector never inspects or transforms message content; it decides
/// only where a boundary may fall so that a cut never splits an assistant
/// part, separates a tool call from its terminal result, or separates an
/// admitted input from its promotion into history. Implementations must be
/// safe to call concurrently for independent requests and must not mutate
/// <see cref="CompactionSourceSnapshot"/> instances they receive.
/// </remarks>
public interface ICompactionCutSelector
{
    /// <summary>Selects a cut for one compaction attempt.</summary>
    /// <param name="request">The cut-selection request.</param>
    /// <param name="cancellationToken">A token used to cancel the selection.</param>
    /// <returns>
    /// A task that resolves to the closed selection outcome: a selected
    /// cut, a deliberate rejection, or an unexpected failure.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<CompactionCutSelectionResult> SelectAsync(
        CompactionCutSelectionRequest request, CancellationToken cancellationToken = default);
}
