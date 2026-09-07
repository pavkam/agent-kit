// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable base for the per-input outcome of one embedding request,
/// carried within an <see cref="EmbeddingResponse"/>.
/// </summary>
/// <remarks>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="EmbeddingItemSucceeded"/> and <see cref="EmbeddingItemFailed"/>.
/// Its constructor is <see langword="private protected"/>, so no assembly
/// outside AgentKit.Abstractions can add a third kind.
/// </remarks>
public abstract record EmbeddingItemOutcome
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddingItemOutcome"/>
    /// record. This constructor is <see langword="private protected"/> so
    /// only the closed set of kinds declared in this assembly can extend
    /// the hierarchy.
    /// </summary>
    /// <param name="inputIndex">The zero-based position of the input this outcome answers.</param>
    /// <param name="correlationId">The caller-supplied correlation identity of the input, when it had one.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="inputIndex"/> is negative.</exception>
    private protected EmbeddingItemOutcome(int inputIndex, EmbeddingInputId? correlationId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inputIndex);

        InputIndex = inputIndex;
        CorrelationId = correlationId;
    }

    /// <summary>
    /// Gets the zero-based position of the input this outcome answers,
    /// within the originating <see cref="EmbeddingRequest.Inputs"/> array.
    /// Always trust this index over response array order.
    /// </summary>
    public int InputIndex { get; init; }

    /// <summary>Gets the caller-supplied correlation identity of the input, when it had one.</summary>
    public EmbeddingInputId? CorrelationId { get; init; }
}
