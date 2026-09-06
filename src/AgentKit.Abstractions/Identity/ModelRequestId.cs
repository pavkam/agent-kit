// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one model request/attempt context end to end. The same
/// identity flows through context preparation, the provider attempt,
/// streamed response events, the committed <see cref="AssistantMessage"/>'s
/// metadata, diagnostics, and usage accounting, so every artifact produced
/// by one logical request can be traced back to it.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>. It carries no mutable state itself and is safe to
/// share and compare across threads without synchronization.
/// </para>
/// <para>
/// The loop allocates exactly one <see cref="ModelRequestId"/> through the
/// injected <see cref="IIdentifierGenerator{ModelRequestId}"/> before
/// context preparation begins for a request, and that same value is carried
/// into the resulting context manifest, every streamed response event, and
/// the final <see cref="AssistantResponseMetadata.RequestId"/>. Same-model
/// retries performed by the provider request executor reuse this identity
/// across attempts; a genuinely new request (for example, after falling
/// back to a different model) gets a new <see cref="ModelRequestId"/>.
/// </para>
/// <para>
/// As with every AgentKit identity struct, the CLR default value (an
/// all-zero GUID) can still exist as an uninitialized field's value; this
/// constructor rejects that value for every explicitly constructed instance
/// so a valid <see cref="ModelRequestId"/> always addresses a real request.
/// </para>
/// </remarks>
public readonly record struct ModelRequestId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ModelRequestId"/>
    /// struct, validating that it addresses a real request rather than an
    /// empty placeholder.
    /// </summary>
    /// <param name="value">The non-empty underlying globally unique identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="Guid.Empty"/>, which can never
    /// address a real request.
    /// </exception>
    public ModelRequestId(Guid value)
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
