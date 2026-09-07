// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One input that was successfully embedded.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </remarks>
public sealed record EmbeddingItemSucceeded: EmbeddingItemOutcome
{
    /// <summary>Initializes a new instance of the <see cref="EmbeddingItemSucceeded"/> record.</summary>
    /// <param name="inputIndex">The zero-based position of the input this outcome answers.</param>
    /// <param name="correlationId">The caller-supplied correlation identity of the input, when it had one.</param>
    /// <param name="vector">The computed embedding vector.</param>
    /// <param name="space">The identity that determines which other vectors this one is comparable to.</param>
    /// <param name="extensions">Provider-specific per-item data.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="vector"/>, <paramref name="space"/>, or
    /// <paramref name="extensions"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="inputIndex"/> is negative.</exception>
    public EmbeddingItemSucceeded(
        int inputIndex,
        EmbeddingInputId? correlationId,
        EmbeddingVector vector,
        EmbeddingSpaceIdentity space,
        ExtensionData extensions)
        : base(inputIndex, correlationId)
    {
        ArgumentNullException.ThrowIfNull(vector);
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(extensions);

        Vector = vector;
        Space = space;
        Extensions = extensions;
    }

    /// <summary>Gets the computed embedding vector.</summary>
    public EmbeddingVector Vector { get; init; }

    /// <summary>Gets the identity that determines which other vectors this one is comparable to.</summary>
    public EmbeddingSpaceIdentity Space { get; init; }

    /// <summary>Gets provider-specific per-item data.</summary>
    public ExtensionData Extensions { get; init; }
}
