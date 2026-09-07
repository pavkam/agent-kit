// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable base for the terminal outcome of one
/// <see cref="IEmbeddingModel"/> attempt, returned from
/// <see cref="IEmbeddingModel.GenerateAsync"/>.
/// </summary>
/// <remarks>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="EmbeddingAttemptCompleted"/>,
/// <see cref="EmbeddingAttemptFailed"/>, and
/// <see cref="EmbeddingAttemptCancelled"/>. Its constructor is
/// <see langword="private protected"/>, so no assembly outside
/// AgentKit.Abstractions can add a fourth kind.
/// </remarks>
public abstract record EmbeddingAttemptResult
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="EmbeddingAttemptResult"/> record. This constructor is
    /// <see langword="private protected"/> so only the closed set of kinds
    /// declared in this assembly can extend the hierarchy.
    /// </summary>
    private protected EmbeddingAttemptResult()
    {
    }
}
