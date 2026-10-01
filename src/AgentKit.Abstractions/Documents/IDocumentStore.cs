// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Owns versioned source documents, their chunk sets, and the atomic active-version pointer behind an exact security boundary.</summary>
/// <remarks>
/// <para>
/// A store preserves source material metadata, authorization, versions, and content identity. A source update publishes a
/// complete versioned chunk set and switches the active-version pointer atomically, so a reader never sees stale and current
/// chunks as one source. Deletion commits an authoritative tombstone before physical removal.
/// </para>
/// <para>
/// Every operation consumes a single-use <see cref="SecurityGrant"/> that binds that exact operation, and resolves identities
/// only inside the authorized tenant, agent, and principal visibility. Implementations are thread-safe.
/// </para>
/// </remarks>
public interface IDocumentStore
{
    /// <summary>Gets the store's identity and capability claims.</summary>
    /// <value>An immutable descriptor that does not change after construction.</value>
    public DocumentStoreDescriptor Descriptor { get; }

    /// <summary>Publishes one complete versioned chunk set, optionally switching the active pointer in the same step.</summary>
    /// <param name="request">The exact authorized request.</param>
    /// <param name="cancellationToken">Cancels before the write commits.</param>
    /// <returns>The stored version or a typed refusal; grant denial happens before any write.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the write commits.</exception>
    public ValueTask<DocumentWriteResult> WriteAsync(DocumentWriteRequest request, CancellationToken cancellationToken = default);

    /// <summary>Atomically switches a document's active-version pointer to a staged version.</summary>
    /// <param name="request">The exact authorized request.</param>
    /// <param name="cancellationToken">Cancels before the switch commits.</param>
    /// <returns>The newly active version or a typed refusal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the switch commits.</exception>
    public ValueTask<DocumentActivateResult> ActivateAsync(DocumentActivateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reads one document version, resolving the active version when none is named.</summary>
    /// <param name="request">The exact authorized request.</param>
    /// <param name="cancellationToken">Cancels before the read completes.</param>
    /// <returns>The version with its chunks, the tombstone of a deleted document, or a typed refusal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<DocumentReadResult> ReadAsync(DocumentReadRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes one document by committing a tombstone and, when requested, physically purging its chunks.</summary>
    /// <param name="request">The exact authorized request.</param>
    /// <param name="cancellationToken">Cancels before the write commits.</param>
    /// <returns>A receipt separating logical invisibility from physical purge, or a typed refusal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the write commits.</exception>
    public ValueTask<DocumentDeleteResult> DeleteAsync(DocumentDeleteRequest request, CancellationToken cancellationToken = default);
}
