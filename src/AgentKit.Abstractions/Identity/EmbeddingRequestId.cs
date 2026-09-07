// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one embedding request/attempt end to end, so every diagnostic,
/// log entry, and result produced by one logical embedding call can be
/// traced back to it.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>. It carries no mutable state itself and is safe to
/// share and compare across threads without synchronization.
/// </para>
/// <para>
/// This is a distinct identity type from <see cref="ModelRequestId"/>, not a
/// reuse of it, because embedding generation is a separate provider
/// contract from conversational generation with its own correlation space;
/// conflating the two would make it impossible to tell a chat request from
/// an embedding request by identity alone.
/// </para>
/// <para>
/// As with every AgentKit identity struct, the CLR default value (an
/// all-zero GUID) can still exist as an uninitialized field's value; this
/// constructor rejects that value for every explicitly constructed instance
/// so a valid <see cref="EmbeddingRequestId"/> always addresses a real
/// request.
/// </para>
/// </remarks>
public readonly record struct EmbeddingRequestId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddingRequestId"/>
    /// struct, validating that it addresses a real request rather than an
    /// empty placeholder.
    /// </summary>
    /// <param name="value">The non-empty underlying globally unique identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="Guid.Empty"/>, which can never
    /// address a real request.
    /// </exception>
    public EmbeddingRequestId(Guid value)
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
    /// identity (GUID "D" format), suitable for logging, storage keys, and
    /// round-tripping through configuration or external protocols.
    /// </summary>
    public override string ToString() => Value.ToString("D");
}
