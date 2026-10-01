// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Produces the protected resources and canonical fingerprints that bind a security grant to one exact document-store operation.</summary>
/// <remarks>
/// A document store derives the same resource and fingerprint from the request it is executing and asks the grant store to
/// consume a grant that binds them, so a grant issued for one document, version, or chunk set cannot authorize another.
/// Reads use <see cref="SecurityOperationKind.StateRead"/> and writes use <see cref="SecurityOperationKind.StateMutation"/>.
/// Payloads are built from identities, hashes, and counts only, so a fingerprint never depends on document content.
/// </remarks>
public static class DocumentSecurityBinding
{
    /// <summary>Names one document as a protected resource.</summary>
    /// <param name="id">The document identity.</param>
    /// <returns>The protected document resource.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default.</exception>
    public static ProtectedResource Resource(DocumentId id)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        return new(ProtectedResourceKind.ApplicationState, $"document:{id}");
    }

    /// <summary>Fingerprints a version publication.</summary>
    /// <param name="record">The version's record.</param>
    /// <param name="chunks">The complete chunk set.</param>
    /// <param name="activate">Whether the pointer switches atomically.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <param name="at">The publication instant.</param>
    /// <returns>A deterministic fingerprint of the record identity, content hash, chunk identities, and flags.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="chunks"/> is default or contains null, or the key is blank.</exception>
    public static InputFingerprint WriteFingerprint(DocumentRecord record, ImmutableArray<DocumentChunk> chunks, bool activate, IdempotencyKey idempotencyKey, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfDefault(chunks);
        ArgumentException.ThrowIfContainsNull(chunks);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        return SecurityCanonicalFingerprint.Create(new WritePayload(
            record.Id.Value,
            record.Version.Value,
            record.ContentHash.Value,
            record.AgentId.Value,
            record.TenantId.Value,
            record.Classification,
            [.. chunks.Select(static chunk => $"{chunk.Id.Value:N}:{chunk.Ordinal}:{chunk.Hash.Value}")],
            activate,
            idempotencyKey.Value,
            at));
    }

    /// <summary>Fingerprints an active-version switch.</summary>
    /// <param name="id">The document identity.</param>
    /// <param name="version">The version to activate.</param>
    /// <param name="expectedActiveVersion">The version that must currently be active, or <see langword="null"/>.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <param name="at">The activation instant.</param>
    /// <returns>A deterministic fingerprint of the switch.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default.</exception>
    /// <exception cref="ArgumentException">A version or key is blank.</exception>
    public static InputFingerprint ActivateFingerprint(DocumentId id, DocumentVersion version, DocumentVersion? expectedActiveVersion, IdempotencyKey idempotencyKey, DateTimeOffset at)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(version.Value, nameof(version));
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        return SecurityCanonicalFingerprint.Create(new ActivatePayload(id.Value, version.Value, expectedActiveVersion?.Value ?? string.Empty, idempotencyKey.Value, at));
    }

    /// <summary>Fingerprints a document read.</summary>
    /// <param name="id">The document identity.</param>
    /// <param name="version">The exact version, or <see langword="null"/> for the active version.</param>
    /// <param name="includeChunks">Whether chunks are returned.</param>
    /// <returns>A deterministic fingerprint of the read.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default.</exception>
    public static InputFingerprint ReadFingerprint(DocumentId id, DocumentVersion? version, bool includeChunks)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        return SecurityCanonicalFingerprint.Create(new ReadPayload(id.Value, version?.Value ?? string.Empty, includeChunks));
    }

    /// <summary>Fingerprints a document deletion.</summary>
    /// <param name="id">The document identity.</param>
    /// <param name="mode">How far the deletion goes.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <param name="at">The deletion instant.</param>
    /// <returns>A deterministic fingerprint of the deletion.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default.</exception>
    /// <exception cref="ArgumentException">The key is blank.</exception>
    public static InputFingerprint DeleteFingerprint(DocumentId id, DocumentDeleteMode mode, IdempotencyKey idempotencyKey, DateTimeOffset at)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        return SecurityCanonicalFingerprint.Create(new DeletePayload(id.Value, mode, idempotencyKey.Value, at));
    }

    private sealed record WritePayload(
        Guid Id,
        string Version,
        string ContentHash,
        Guid AgentId,
        string TenantId,
        DataClassification Classification,
        ImmutableArray<string> Chunks,
        bool Activate,
        string IdempotencyKey,
        DateTimeOffset At);

    private sealed record ActivatePayload(Guid Id, string Version, string ExpectedActiveVersion, string IdempotencyKey, DateTimeOffset At);

    private sealed record ReadPayload(Guid Id, string Version, bool IncludeChunks);

    private sealed record DeletePayload(Guid Id, DocumentDeleteMode Mode, string IdempotencyKey, DateTimeOffset At);
}
