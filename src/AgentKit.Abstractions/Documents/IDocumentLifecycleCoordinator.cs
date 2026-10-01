// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Publishes and removes source documents across the document store and every vector index of a memory profile.</summary>
/// <remarks>
/// <para>
/// Publication stages a version's chunks inactive, embeds them, upserts their vectors into every profile index that shares the
/// embedding's vector space, and only then switches the document's active-version pointer. Until that final step the previous
/// version stays the only active one. After the switch the superseded version's vectors are removed on a best-effort basis, and
/// retrieval drops any hit whose version is not the active one, so stale and current chunks are never both retrievable.
/// </para>
/// <para>
/// Deletion commits the document tombstone first, removes the chunk vectors named by the receipt from every profile index, and
/// purges the document body last. A vector index that could not be cleaned is reported by name in the receipt rather than hidden,
/// and the purge is withheld until every index is clean.
/// </para>
/// <para>Implementations ask the security authority named by the operation context for one single-use grant per store call; the coordinator never holds authority of its own.</para>
/// </remarks>
public interface IDocumentLifecycleCoordinator
{
    /// <summary>Publishes one document version and makes it the active version.</summary>
    /// <param name="command">The validated publication command.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The published record or a typed refusal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> is null.</exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    public ValueTask<DocumentPublishResult> PublishAsync(DocumentPublishCommand command, CancellationToken cancellationToken = default);

    /// <summary>Deletes one document and propagates the deletion to every vector index of the profile.</summary>
    /// <param name="command">The validated removal command.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The final deletion receipt or a typed refusal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> is null.</exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    public ValueTask<DocumentRemovalResult> DeleteAsync(DocumentRemovalCommand command, CancellationToken cancellationToken = default);
}
