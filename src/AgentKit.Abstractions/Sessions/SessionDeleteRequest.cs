// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A request to delete one session and its entire record.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </remarks>
public sealed record SessionDeleteRequest
{
    /// <summary>Initializes a new instance of the <see cref="SessionDeleteRequest"/> record.</summary>
    /// <param name="context">The operation context for this deletion.</param>
    /// <param name="idempotencyKey">
    /// The key that makes repeating this exact request safe: a retry after
    /// a successful delete still reports success rather than a not-found
    /// failure.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public SessionDeleteRequest(SessionOperationContext context, IdempotencyKey idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(context);
        Context = context;
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>Gets the operation context for this deletion.</summary>
    public SessionOperationContext Context { get; }

    /// <summary>
    /// Gets the key that makes repeating this exact request safe.
    /// </summary>
    public IdempotencyKey IdempotencyKey { get; }
}
