// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An optional, caller-supplied correlation identity for one
/// <see cref="EmbeddingInput"/> within an <see cref="EmbeddingRequest"/>.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>. It carries no mutable state itself and is safe to
/// share and compare across threads without synchronization.
/// </para>
/// <para>
/// The provider-returned or positional <c>InputIndex</c> on
/// <see cref="EmbeddingItemOutcome"/> is the primary, always-available
/// correlation mechanism between a request input and its result; this
/// identity is a supplementary caller correlation key layered on top of it,
/// never a replacement for checking the index.
/// </para>
/// </remarks>
public readonly record struct EmbeddingInputId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddingInputId"/>
    /// struct, validating that it addresses a real input rather than an
    /// empty placeholder.
    /// </summary>
    /// <param name="value">The non-empty underlying globally unique identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="Guid.Empty"/>, which can never
    /// address a real input.
    /// </exception>
    public EmbeddingInputId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty, nameof(value));
        Value = value;
    }

    /// <summary>
    /// Gets the underlying globally unique identifier. This is the only
    /// state the type carries, and it never changes after construction.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Returns the canonical, lowercase, hyphenated text form of this
    /// identity (GUID "D" format), suitable for logging and round-tripping
    /// through configuration or external protocols.
    /// </summary>
    public override string ToString() => Value.ToString("D");
}
