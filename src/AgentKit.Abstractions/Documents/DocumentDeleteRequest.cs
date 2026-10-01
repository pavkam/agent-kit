// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks a document store to delete one document with every version.</summary>
/// <remarks>Deletion commits the tombstone and clears the active-version pointer before physical removal. Deleting an already deleted document replays its receipt, and a purge may follow a tombstone.</remarks>
public sealed record DocumentDeleteRequest
{
    /// <summary>Initializes a validated delete request.</summary>
    /// <param name="id">The document to delete.</param>
    /// <param name="mode">How far the deletion goes.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <param name="at">The deletion instant, taken from the injected clock.</param>
    /// <param name="grant">The single-use grant for this exact operation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default or <paramref name="mode"/> is undefined.</exception>
    /// <exception cref="ArgumentException">The key is blank or the grant lacks captured authorization.</exception>
    public DocumentDeleteRequest(DocumentId id, DocumentDeleteMode mode, IdempotencyKey idempotencyKey, DateTimeOffset at, SecurityGrant grant)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
        ArgumentOutOfRangeException.ThrowIfUndefined(mode);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(grant.Authorization, nameof(grant));
        Id = id;
        Mode = mode;
        IdempotencyKey = idempotencyKey;
        At = at;
        Grant = grant;
    }

    /// <summary>Gets the document to delete.</summary>
    public DocumentId Id { get; }

    /// <summary>Gets how far the deletion goes.</summary>
    public DocumentDeleteMode Mode { get; }

    /// <summary>Gets the replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }

    /// <summary>Gets the deletion instant.</summary>
    public DateTimeOffset At { get; }

    /// <summary>Gets the single-use grant for this exact operation.</summary>
    public SecurityGrant Grant { get; }
}
