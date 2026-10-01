// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks a memory store to delete one record.</summary>
/// <remarks>
/// Deletion commits the tombstone before physical removal, and deleting an already deleted record replays its receipt. A
/// non-null <see cref="ExpectedVersion"/> is compared only while the record is still live. The <see cref="Grant"/> is
/// single-use and binds this exact operation.
/// </remarks>
public sealed record MemoryDeleteRequest
{
    /// <summary>Initializes a validated delete request.</summary>
    /// <param name="id">The record to delete.</param>
    /// <param name="expectedVersion">The version the live record must have, or <see langword="null"/> to delete whatever version is stored.</param>
    /// <param name="mode">How far the deletion goes.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <param name="at">The deletion instant, taken from the injected clock.</param>
    /// <param name="grant">The single-use grant for this exact operation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default or <paramref name="mode"/> is undefined.</exception>
    /// <exception cref="ArgumentException">A version or key is blank, or the grant lacks captured authorization.</exception>
    public MemoryDeleteRequest(
        MemoryId id,
        VersionToken? expectedVersion,
        MemoryDeleteMode mode,
        IdempotencyKey idempotencyKey,
        DateTimeOffset at,
        SecurityGrant grant)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
        if (expectedVersion is { } version)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(version.Value, nameof(expectedVersion));
        }

        ArgumentOutOfRangeException.ThrowIfUndefined(mode);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(grant.Authorization, nameof(grant));
        Id = id;
        ExpectedVersion = expectedVersion;
        Mode = mode;
        IdempotencyKey = idempotencyKey;
        At = at;
        Grant = grant;
    }

    /// <summary>Gets the record to delete.</summary>
    public MemoryId Id { get; }

    /// <summary>Gets the version the live record must have, or <see langword="null"/>.</summary>
    public VersionToken? ExpectedVersion { get; }

    /// <summary>Gets how far the deletion goes.</summary>
    public MemoryDeleteMode Mode { get; }

    /// <summary>Gets the replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }

    /// <summary>Gets the deletion instant.</summary>
    public DateTimeOffset At { get; }

    /// <summary>Gets the single-use grant for this exact operation.</summary>
    public SecurityGrant Grant { get; }
}
