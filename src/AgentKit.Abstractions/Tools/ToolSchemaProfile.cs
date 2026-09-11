// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares one immutable local tool-schema dialect and its exact supported keyword sets.</summary>
/// <remarks>A profile may support a bounded subset of a dialect. Unknown keywords must reject compilation; they are never silently removed from canonical validation. Profile equality is structural and order-independent for keyword sets.</remarks>
public sealed record ToolSchemaProfile
{
    /// <summary>Creates an explicit versioned profile with disjoint keyword classifications.</summary>
    /// <param name="id">The nondefault profile family identity.</param>
    /// <param name="version">The positive semantic revision.</param>
    /// <param name="dialect">The nondefault exact supported schema dialect.</param>
    /// <param name="assertionKeywords">Initialized unique nonblank assertion and applicator names.</param>
    /// <param name="annotationKeywords">Initialized unique nonblank annotation names disjoint from assertions.</param>
    /// <exception cref="ArgumentException">An identity or keyword collection is invalid or keywords overlap.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is default.</exception>
    public ToolSchemaProfile(ToolSchemaProfileId id, ToolSchemaProfileVersion version, JsonSchemaDialectId dialect,
        ImmutableArray<string> assertionKeywords, ImmutableArray<string> annotationKeywords)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id.Value, nameof(id));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version.Value, nameof(version));
        ArgumentException.ThrowIfNullOrWhiteSpace(dialect.Value, nameof(dialect));
        ArgumentException.ThrowIfContainsNull(assertionKeywords);
        ArgumentException.ThrowIfContainsNull(annotationKeywords);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in assertionKeywords)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(assertionKeywords));
            ArgumentException.ThrowIfNotEqual(names.Add(name), true, nameof(assertionKeywords));
        }
        foreach (var name in annotationKeywords)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(annotationKeywords));
            ArgumentException.ThrowIfNotEqual(names.Add(name), true, nameof(annotationKeywords));
        }
        Id = id; Version = version; Dialect = dialect;
        AssertionKeywords = [.. assertionKeywords.Order(StringComparer.Ordinal)];
        AnnotationKeywords = [.. annotationKeywords.Order(StringComparer.Ordinal)];
    }
    /// <summary>Gets the exact profile family identity.</summary>
    /// <value>The nondefault authored family.</value>
    public ToolSchemaProfileId Id { get; }
    /// <summary>Gets the revision pinning keyword and work-accounting semantics.</summary>
    /// <value>The positive authored revision.</value>
    public ToolSchemaProfileVersion Version { get; }
    /// <summary>Gets the exact dialect accepted by this profile.</summary>
    /// <value>The nondefault dialect; support is limited to the declared keyword sets.</value>
    public JsonSchemaDialectId Dialect { get; }
    /// <summary>Gets supported assertions and applicators in ordinal order.</summary>
    /// <value>An initialized immutable set; schema declarations are handled separately.</value>
    public ImmutableArray<string> AssertionKeywords { get; }
    /// <summary>Gets supported annotations in ordinal order.</summary>
    /// <value>An initialized immutable set whose data never applies defaults or grants authority.</value>
    public ImmutableArray<string> AnnotationKeywords { get; }
    /// <summary>Compares exact profile identity, dialect, revision, and keyword sets.</summary>
    /// <param name="other">The profile to compare, or null.</param>
    /// <returns>True only when all retained capability evidence agrees.</returns>
    public bool Equals(ToolSchemaProfile? other) => other is not null && Id == other.Id && Version == other.Version
        && Dialect == other.Dialect && AssertionKeywords.SequenceEqual(other.AssertionKeywords)
        && AnnotationKeywords.SequenceEqual(other.AnnotationKeywords);
    /// <summary>Hashes immutable profile evidence consistently with structural equality.</summary>
    /// <returns>A hash of the complete normalized profile.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode(); hash.Add(Id); hash.Add(Version); hash.Add(Dialect);
        foreach (var name in AssertionKeywords) { hash.Add(name, StringComparer.Ordinal); }
        foreach (var name in AnnotationKeywords) { hash.Add(name, StringComparer.Ordinal); }
        return hash.ToHashCode();
    }
}
