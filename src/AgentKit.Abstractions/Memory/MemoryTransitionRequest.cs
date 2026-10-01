// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks a memory store to apply one lifecycle transition to one record.</summary>
/// <remarks>
/// A transition is a versioned compare-and-set: it succeeds only when <see cref="ExpectedVersion"/> equals the stored version,
/// and an equivalent replay by <see cref="IdempotencyKey"/> returns the original outcome. A correction
/// (<see cref="MemoryLifecycleState.Corrected"/>) must carry the active replacement record, which the store creates in the same
/// atomic step so history is appended rather than rewritten. Deletion is a separate operation.
/// </remarks>
public sealed record MemoryTransitionRequest
{
    /// <summary>Initializes a validated transition request.</summary>
    /// <param name="id">The record to transition.</param>
    /// <param name="to">The target state; never <see cref="MemoryLifecycleState.Deleted"/>.</param>
    /// <param name="expectedVersion">The version the stored record must currently have.</param>
    /// <param name="replacement">The active replacement record for a correction, or <see langword="null"/> for every other transition.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <param name="at">The transition instant, taken from the injected clock.</param>
    /// <param name="grant">The single-use grant for this exact operation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default or <paramref name="to"/> is undefined.</exception>
    /// <exception cref="ArgumentException">The target is deleted, a correction lacks an active version-1 replacement, another transition carries one, a version or key is blank, or the grant lacks captured authorization.</exception>
    public MemoryTransitionRequest(
        MemoryId id,
        MemoryLifecycleState to,
        VersionToken expectedVersion,
        DurableMemoryRecord? replacement,
        IdempotencyKey idempotencyKey,
        DateTimeOffset at,
        SecurityGrant grant)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
        ArgumentOutOfRangeException.ThrowIfUndefined(to);
        ArgumentException.ThrowIfNotEqual(to == MemoryLifecycleState.Deleted, false, nameof(to));
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedVersion.Value, nameof(expectedVersion));
        ArgumentException.ThrowIfNotEqual(replacement is not null, to == MemoryLifecycleState.Corrected, nameof(replacement));
        if (replacement is not null)
        {
            ArgumentException.ThrowIfNotEqual(replacement.State, MemoryLifecycleState.Active, nameof(replacement));
            ArgumentException.ThrowIfNotEqual(replacement.Version, new VersionToken("1"), nameof(replacement));
            ArgumentException.ThrowIfNotEqual(replacement.Id == id, false, nameof(replacement));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(grant.Authorization, nameof(grant));
        Id = id;
        To = to;
        ExpectedVersion = expectedVersion;
        Replacement = replacement;
        IdempotencyKey = idempotencyKey;
        At = at;
        Grant = grant;
    }

    /// <summary>Gets the record to transition.</summary>
    public MemoryId Id { get; }

    /// <summary>Gets the target state.</summary>
    public MemoryLifecycleState To { get; }

    /// <summary>Gets the version the stored record must currently have.</summary>
    public VersionToken ExpectedVersion { get; }

    /// <summary>Gets the active replacement record of a correction, or <see langword="null"/>.</summary>
    public DurableMemoryRecord? Replacement { get; }

    /// <summary>Gets the replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }

    /// <summary>Gets the transition instant.</summary>
    public DateTimeOffset At { get; }

    /// <summary>Gets the single-use grant for this exact operation.</summary>
    public SecurityGrant Grant { get; }
}
