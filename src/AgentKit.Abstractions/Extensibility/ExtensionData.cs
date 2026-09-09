// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An immutable, keyed bag of provider-specific or forward-compatible
/// values attached to a message, content part, response, or other AgentKit
/// record. Typed portable fields on the owning record remain first-class
/// members; extension data never replaces them.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="Values"/> (key-by-key, value-by-value). It carries no mutable
/// state and is safe to share and read concurrently from multiple threads;
/// <see cref="ImmutableDictionary{TKey, TValue}"/> guarantees that reads
/// never observe a partially updated collection, and there is no update
/// operation on this type at all — producing a "changed" bag means building
/// a new <see cref="ImmutableDictionary{TKey, TValue}"/> and wrapping it in
/// a new <see cref="ExtensionData"/> instance.
/// </para>
/// <para>
/// Extension data exists to preserve information AgentKit's portable model
/// cannot represent as a first-class field — a provider's raw safety
/// classification, a beta field a newer API version introduced — without
/// silently discarding it. It is not a general-purpose property bag:
/// anything the core needs to reason about (roles, tool correlation, usage,
/// stop reason) is modeled as a real typed member instead, precisely so
/// that behaviorally meaningful data cannot hide inside an opaque
/// dictionary where validation, translation, and policy code would not see
/// it.
/// </para>
/// </remarks>
public sealed record ExtensionData
{
    /// <summary>
    /// Gets the shared, empty <see cref="ExtensionData"/> instance, for use
    /// wherever a record has no provider-specific data to carry. Reusing
    /// this instance instead of constructing a new empty dictionary avoids
    /// needless allocation on the overwhelmingly common "no extensions"
    /// path.
    /// </summary>
    public static ExtensionData Empty { get; } = new(
        []);

    /// <summary>Initializes a new instance of the <see cref="ExtensionData"/> record.</summary>
    /// <param name="values">
    /// The keyed extension values. The caller transfers effective ownership
    /// of the immutable entries. This constructor normalizes the retained
    /// dictionary's key and value comparers, but does not copy individual
    /// keys or values.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is null.</exception>
    public ExtensionData(ImmutableDictionary<string, ExtensionValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Values = values;
    }

    /// <summary>
    /// Gets the keyed extension values. Each key is a stable,
    /// component-defined extension name; callers that do not recognize a
    /// key are expected to preserve it unchanged rather than discard it.
    /// An initializer must supply a non-null dictionary. The value retained
    /// by this record always uses ordinal key comparison and the default
    /// structural <see cref="ExtensionValue"/> comparer, while preserving
    /// every supplied key's original spelling and value.
    /// </summary>
    /// <value>
    /// An immutable ordinal-keyed dictionary whose values use
    /// <see cref="EqualityComparer{T}.Default"/> comparison for
    /// <see cref="ExtensionValue"/>.
    /// </value>
    /// <exception cref="ArgumentNullException">
    /// An object initializer or <c>with</c> expression supplies a null
    /// dictionary.
    /// </exception>
    public ImmutableDictionary<string, ExtensionValue> Values
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Values));
            field = value.WithComparers(StringComparer.Ordinal, EqualityComparer<ExtensionValue>.Default);
        }
    }

    /// <summary>
    /// Determines whether this instance and <paramref name="other"/>
    /// contain the same keys mapped to the same values.
    /// </summary>
    /// <param name="other">The instance to compare against, or <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> if both instances have the same number of
    /// entries and every key in this instance maps to an equal
    /// <see cref="ExtensionValue"/> in <paramref name="other"/>; otherwise
    /// <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// This overrides the record-synthesized equality because
    /// <see cref="ImmutableDictionary{TKey, TValue}"/> does not implement
    /// content-based equality at all — it falls back to reference equality,
    /// so two independently built dictionaries with identical entries would
    /// otherwise never compare equal. Key/value pairs are compared without
    /// regard to enumeration order, since an immutable dictionary does not
    /// guarantee a stable iteration order across independently built
    /// instances with the same logical content.
    /// </remarks>
    public bool Equals(ExtensionData? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (Values.Count != other.Values.Count)
        {
            return false;
        }

        foreach (var (key, value) in Values)
        {
            if (!other.Values.TryGetValue(key, out var otherValue) || !value.Equals(otherValue))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns a hash code consistent with <see cref="Equals(ExtensionData?)"/>:
    /// two instances with the same keys mapped to the same values always
    /// produce the same hash code, regardless of insertion or enumeration
    /// order.
    /// </summary>
    /// <returns>A hash code derived from the contained key/value pairs.</returns>
    public override int GetHashCode()
    {
        var combinedHash = 0;
        foreach (var (key, value) in Values)
        {
            // XOR combination keeps the result independent of enumeration
            // order, which ImmutableDictionary does not guarantee to be
            // stable across independently constructed instances.
            combinedHash ^= HashCode.Combine(key, value);
        }

        return combinedHash;
    }
}
