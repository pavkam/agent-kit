// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Produces the protected resources and canonical fingerprints that bind a security grant to one exact memory-store operation.</summary>
/// <remarks>
/// A memory store derives the same resource and fingerprint from the request it is executing and asks the grant store to
/// consume a grant that binds them, so a grant issued for one record, version, or payload cannot authorize another. Reads use
/// <see cref="SecurityOperationKind.StateRead"/> and writes use <see cref="SecurityOperationKind.StateMutation"/>. The
/// fingerprint methods take the operation's parts, so a coordinator can bind a grant before it builds the store request.
/// </remarks>
public static class MemorySecurityBinding
{
    /// <summary>Names one memory as a protected resource.</summary>
    /// <param name="id">The memory identity.</param>
    /// <returns>The protected memory resource.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default.</exception>
    public static ProtectedResource Resource(MemoryId id)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        return new(ProtectedResourceKind.ApplicationState, $"memory:{id}");
    }

    /// <summary>Names one agent's memory collection as a protected resource, for page reads.</summary>
    /// <param name="agentId">The owning agent.</param>
    /// <returns>The protected collection resource.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="agentId"/> is default.</exception>
    public static ProtectedResource CollectionResource(AgentId agentId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        return new(ProtectedResourceKind.ApplicationState, $"memory-collection:{agentId}");
    }

    /// <summary>Fingerprints a memory creation.</summary>
    /// <param name="record">The record to create.</param>
    /// <param name="idempotencyKey">The creation replay key.</param>
    /// <returns>A deterministic fingerprint of the record and key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="idempotencyKey"/> is blank.</exception>
    public static InputFingerprint WriteFingerprint(DurableMemoryRecord record, IdempotencyKey idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        return SecurityCanonicalFingerprint.Create(new WritePayload(record, idempotencyKey));
    }

    /// <summary>Fingerprints a memory point read.</summary>
    /// <param name="id">The memory identity.</param>
    /// <returns>A deterministic fingerprint of the identity.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default.</exception>
    public static InputFingerprint ReadFingerprint(MemoryId id)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        return SecurityCanonicalFingerprint.Create(new ReadPayload(id));
    }

    /// <summary>Fingerprints a memory page read.</summary>
    /// <param name="namespace">The namespace filter, or <see langword="null"/>.</param>
    /// <param name="states">The lifecycle states, or default for active only.</param>
    /// <param name="terms">The keyword terms, or default for none.</param>
    /// <param name="afterSequence">The exclusive cursor.</param>
    /// <param name="limit">The page size.</param>
    /// <returns>A deterministic fingerprint of the page filter.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The cursor is negative or the limit is not positive.</exception>
    public static InputFingerprint ListFingerprint(
        MemoryNamespace? @namespace,
        ImmutableArray<MemoryLifecycleState> states,
        ImmutableArray<string> terms,
        long afterSequence,
        int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(afterSequence);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        return SecurityCanonicalFingerprint.Create(new ListPayload(
            @namespace?.Value ?? string.Empty,
            [.. (states.IsDefault ? [MemoryLifecycleState.Active] : states).Select(static state => (int) state).Order()],
            [.. (terms.IsDefault ? [] : terms).Order(StringComparer.Ordinal)],
            afterSequence,
            limit));
    }

    /// <summary>Fingerprints a lifecycle transition.</summary>
    /// <param name="id">The record identity.</param>
    /// <param name="to">The target state.</param>
    /// <param name="expectedVersion">The expected version.</param>
    /// <param name="replacement">The replacement record of a correction, or <see langword="null"/>.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <param name="at">The transition instant.</param>
    /// <returns>A deterministic fingerprint of the transition.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default.</exception>
    /// <exception cref="ArgumentException">A version or key is blank.</exception>
    public static InputFingerprint TransitionFingerprint(
        MemoryId id,
        MemoryLifecycleState to,
        VersionToken expectedVersion,
        DurableMemoryRecord? replacement,
        IdempotencyKey idempotencyKey,
        DateTimeOffset at)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedVersion.Value, nameof(expectedVersion));
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        return SecurityCanonicalFingerprint.Create(new TransitionPayload(id, to, expectedVersion, replacement, idempotencyKey, at));
    }

    /// <summary>Fingerprints a deletion.</summary>
    /// <param name="id">The record identity.</param>
    /// <param name="expectedVersion">The expected live version, or <see langword="null"/>.</param>
    /// <param name="mode">How far the deletion goes.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <param name="at">The deletion instant.</param>
    /// <returns>A deterministic fingerprint of the deletion.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default.</exception>
    /// <exception cref="ArgumentException">A version or key is blank.</exception>
    public static InputFingerprint DeleteFingerprint(
        MemoryId id,
        VersionToken? expectedVersion,
        MemoryDeleteMode mode,
        IdempotencyKey idempotencyKey,
        DateTimeOffset at)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        return SecurityCanonicalFingerprint.Create(new DeletePayload(id, expectedVersion?.Value ?? string.Empty, mode, idempotencyKey, at));
    }

    private sealed record WritePayload(DurableMemoryRecord Record, IdempotencyKey IdempotencyKey);

    private sealed record ReadPayload(MemoryId Id);

    private sealed record ListPayload(string Namespace, ImmutableArray<int> States, ImmutableArray<string> Terms, long AfterSequence, int Limit);

    private sealed record TransitionPayload(
        MemoryId Id,
        MemoryLifecycleState To,
        VersionToken ExpectedVersion,
        DurableMemoryRecord? Replacement,
        IdempotencyKey IdempotencyKey,
        DateTimeOffset At);

    private sealed record DeletePayload(MemoryId Id, string ExpectedVersion, MemoryDeleteMode Mode, IdempotencyKey IdempotencyKey, DateTimeOffset At);
}
