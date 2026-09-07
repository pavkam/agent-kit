// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An attempt that was cancelled before completion, typically because the
/// caller's cancellation token was triggered.
/// </summary>
public sealed record EmbeddingAttemptCancelled: EmbeddingAttemptResult
{
    /// <summary>Initializes a new instance of the <see cref="EmbeddingAttemptCancelled"/> record.</summary>
    /// <param name="cancellation">The normalized cancellation failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="cancellation"/> is null.</exception>
    public EmbeddingAttemptCancelled(ProviderFailure cancellation)
    {
        ArgumentNullException.ThrowIfNull(cancellation);
        Cancellation = cancellation;
    }

    /// <summary>Gets the normalized cancellation failure.</summary>
    public ProviderFailure Cancellation { get; init; }
}
