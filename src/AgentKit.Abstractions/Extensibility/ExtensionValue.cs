// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One bounded, immutable, JSON-compatible value stored inside an
/// <see cref="ExtensionData"/> bag — for example, a provider's raw safety
/// classification or an unmapped field a newer API version added.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="CanonicalJson"/> (byte-for-byte). It carries no mutable state
/// and is safe to share and compare across threads without synchronization.
/// </para>
/// <para>
/// An extension value owns its canonical UTF-8 JSON bytes outright: it
/// never retains a caller-owned buffer, a mutable
/// <c>System.Text.Json.Nodes.JsonNode</c>, or a disposable
/// <see cref="JsonDocument"/> that could later be disposed
/// out from under it. Whoever constructs an <see cref="ExtensionValue"/> is
/// expected to have already produced a stable, self-contained byte
/// sequence — typically by serializing a value once and copying the result
/// into an <see cref="ImmutableArray{Byte}"/> — rather than pointing at
/// memory something else might still mutate.
/// </para>
/// <para>
/// Extension data exists for genuinely provider-specific or
/// forward-compatible content only. A field that AgentKit understands and
/// needs to reason about belongs in a typed, first-class property instead;
/// extension data is not a general-purpose way to avoid modeling a value
/// properly.
/// </para>
/// </remarks>
public readonly record struct ExtensionValue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExtensionValue"/>
    /// struct.
    /// </summary>
    /// <param name="canonicalJson">
    /// The UTF-8 canonical JSON representation of the value. The caller
    /// transfers effective ownership of these bytes; they must not be
    /// mutated after this call, which is naturally the case for an
    /// <see cref="ImmutableArray{Byte}"/>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="canonicalJson"/> is a default, uninitialized array.
    /// </exception>
    public ExtensionValue(ImmutableArray<byte> canonicalJson)
    {
        ArgumentException.ThrowIfDefault(canonicalJson);
        CanonicalJson = canonicalJson;
    }

    /// <summary>
    /// Gets the UTF-8 canonical JSON representation of the value. This is
    /// the only state the type carries, and it never changes after
    /// construction.
    /// </summary>
    public ImmutableArray<byte> CanonicalJson { get; }

    /// <summary>
    /// Determines whether this instance and <paramref name="other"/>
    /// represent the same canonical JSON bytes.
    /// </summary>
    /// <param name="other">The instance to compare against.</param>
    /// <returns>
    /// <see langword="true"/> if <see cref="CanonicalJson"/> is
    /// byte-for-byte identical on both instances; otherwise
    /// <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// This overrides the record-synthesized equality because
    /// <see cref="ImmutableArray{T}"/> implements
    /// <see cref="IEquatable{T}"/> by comparing whether two values wrap the
    /// <em>same underlying array instance</em>, not by comparing their
    /// contents. Two <see cref="ExtensionValue"/> instances built
    /// independently from byte-for-byte identical JSON must still compare
    /// equal, so this method performs an explicit sequence comparison
    /// instead of delegating to the default field-by-field record
    /// comparison.
    /// </remarks>
    public bool Equals(ExtensionValue other) =>
        CanonicalJson.AsSpan().SequenceEqual(other.CanonicalJson.AsSpan());

    /// <summary>
    /// Returns a hash code consistent with <see cref="Equals(ExtensionValue)"/>:
    /// two instances with byte-for-byte identical <see cref="CanonicalJson"/>
    /// always produce the same hash code.
    /// </summary>
    /// <returns>A hash code derived from the canonical JSON bytes.</returns>
    public override int GetHashCode()
    {
        var hashCode = default(HashCode);
        hashCode.AddBytes(CanonicalJson.AsSpan());
        return hashCode.ToHashCode();
    }
}
