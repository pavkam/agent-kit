// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A bounded, JSON-compatible representation of provider content the core
/// cannot interpret. Its stable type name lets a component retain the
/// content without claiming support for it.
/// </summary>
/// <remarks>
/// This is the escape hatch that keeps <see cref="ContentPart"/> a closed
/// hierarchy while still tolerating provider evolution: rather than every
/// new, provider-specific content shape requiring a new subclass (and a
/// coordinated release of every consumer), an adapter that encounters a
/// shape it does not recognize wraps it in an
/// <see cref="UnknownContentPart"/> so it survives storage round trips and
/// compatible provider continuation paths unchanged, ready for a future
/// version of the adapter (or a specialized consumer that does understand
/// <see cref="TypeName"/>) to interpret it properly.
/// </remarks>
public sealed record UnknownContentPart: ContentPart
{
    /// <summary>Initializes a new instance of the <see cref="UnknownContentPart"/> record.</summary>
    /// <param name="typeName">A stable, provider-defined type name for the payload.</param>
    /// <param name="payload">The raw JSON-compatible payload.</param>
    /// <param name="extensions">Provider-specific or forward-compatible data.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="typeName"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public UnknownContentPart(string typeName, JsonElement payload, ExtensionData extensions)
        : base(extensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        TypeName = typeName;
        Payload = payload;
    }

    /// <summary>Gets a stable, provider-defined type name for the payload.</summary>
    public string TypeName { get; init; }

    /// <summary>Gets the raw JSON-compatible payload.</summary>
    public JsonElement Payload { get; init; }

    /// <summary>
    /// Compares this part structurally: <see cref="Payload"/> is compared by JSON value
    /// (<see cref="JsonElement.DeepEquals"/>) rather than by backing-document identity.
    /// </summary>
    /// <param name="other">The part to compare with.</param>
    /// <returns><see langword="true"/> when the type name, JSON payload, and extensions are equal.</returns>
    public bool Equals(UnknownContentPart? other) =>
        other is not null
        && string.Equals(TypeName, other.TypeName, StringComparison.Ordinal)
        && JsonElementValueEquality.Equals(Payload, other.Payload)
        && Extensions.Equals(other.Extensions);

    /// <summary>Hashes the non-JSON members only, consistent with <see cref="Equals(UnknownContentPart?)"/>.</summary>
    /// <returns>A hash consistent with structural equality.</returns>
    public override int GetHashCode() => HashCode.Combine(TypeName, Extensions);
}
