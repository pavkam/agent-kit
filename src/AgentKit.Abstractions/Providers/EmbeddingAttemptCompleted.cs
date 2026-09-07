// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An attempt that completed successfully, carrying the committed
/// response.
/// </summary>
public sealed record EmbeddingAttemptCompleted: EmbeddingAttemptResult
{
    /// <summary>Initializes a new instance of the <see cref="EmbeddingAttemptCompleted"/> record.</summary>
    /// <param name="response">The committed response.</param>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is null.</exception>
    public EmbeddingAttemptCompleted(EmbeddingResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        Response = response;
    }

    /// <summary>Gets the committed response.</summary>
    public EmbeddingResponse Response { get; init; }
}
