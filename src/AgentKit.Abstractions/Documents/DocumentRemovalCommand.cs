// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks the lifecycle coordinator to delete one document and propagate the deletion to every vector index.</summary>
/// <remarks>The command carries an operation context rather than a grant: the coordinator selects the authority the context's authorization names and asks for single-use grants bound to each exact store operation.</remarks>
public sealed record DocumentRemovalCommand
{
    /// <summary>Initializes a validated command.</summary>
    /// <param name="context">The operation context the deletion runs under.</param>
    /// <param name="id">The document to delete.</param>
    /// <param name="mode">How far the deletion goes.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default or <paramref name="mode"/> is undefined.</exception>
    /// <exception cref="ArgumentException">The key is blank.</exception>
    public DocumentRemovalCommand(MemoryOperationContext context, DocumentId id, DocumentDeleteMode mode, IdempotencyKey idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
        ArgumentOutOfRangeException.ThrowIfUndefined(mode);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        Context = context;
        Id = id;
        Mode = mode;
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>Gets the operation context the deletion runs under.</summary>
    public MemoryOperationContext Context { get; }

    /// <summary>Gets the document to delete.</summary>
    public DocumentId Id { get; }

    /// <summary>Gets how far the deletion goes.</summary>
    public DocumentDeleteMode Mode { get; }

    /// <summary>Gets the replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }
}
