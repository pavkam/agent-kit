// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An attempt that failed before the provider returned any per-item
/// result, such as a transport error, an authentication failure, or a
/// non-success HTTP status covering the whole request.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. Unlike <see cref="ModelAttemptFailed"/>,
/// this carries no partial content: embedding generation is not streamed,
/// so a whole-request failure means no item in the batch ever received a
/// result. A provider that reports some inputs as individually failed
/// within an otherwise successful response instead produces an
/// <see cref="EmbeddingAttemptCompleted"/> whose response contains one or
/// more <see cref="EmbeddingItemFailed"/> items.
/// </remarks>
public sealed record EmbeddingAttemptFailed: EmbeddingAttemptResult
{
    /// <summary>Initializes a new instance of the <see cref="EmbeddingAttemptFailed"/> record.</summary>
    /// <param name="failure">The normalized failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public EmbeddingAttemptFailed(ProviderFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets the normalized failure.</summary>
    public ProviderFailure Failure { get; init; }
}
