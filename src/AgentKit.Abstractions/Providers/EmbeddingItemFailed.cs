// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One input that the provider reported as individually failed within an
/// otherwise successful batch response.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. A failure of the entire request (a non-success
/// HTTP status, a transport error) is not represented this way; it produces
/// an <see cref="EmbeddingAttemptFailed"/> instead, since in that case no
/// item in the batch received a result at all.
/// </remarks>
public sealed record EmbeddingItemFailed: EmbeddingItemOutcome
{
    /// <summary>Initializes a new instance of the <see cref="EmbeddingItemFailed"/> record.</summary>
    /// <param name="inputIndex">The zero-based position of the input this outcome answers.</param>
    /// <param name="correlationId">The caller-supplied correlation identity of the input, when it had one.</param>
    /// <param name="failure">The normalized per-item failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="inputIndex"/> is negative.</exception>
    public EmbeddingItemFailed(int inputIndex, EmbeddingInputId? correlationId, ProviderFailure failure)
        : base(inputIndex, correlationId)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets the normalized per-item failure.</summary>
    public ProviderFailure Failure { get; init; }
}
