// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares the immutable dialect and vocabulary capabilities of one local schema engine.</summary>
public sealed record OutputSchemaEngineProfile
{
    /// <summary>Initializes an immutable schema-engine capability profile.</summary>
    /// <param name="id">The initialized profile identity.</param>
    /// <param name="version">The positive profile version.</param>
    /// <param name="defaultDialect">The supported default dialect.</param>
    /// <param name="supportedDialects">The initialized nonempty set of supported dialects.</param>
    /// <param name="assertionKeywords">The initialized unique assertion-keyword set.</param>
    /// <param name="annotationKeywords">The initialized unique annotation-keyword set disjoint from assertions.</param>
    /// <exception cref="ArgumentException">A supplied identity is invalid or the profile sets are inconsistent.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is not positive.</exception>
    public OutputSchemaEngineProfile(OutputSchemaProfileId id, OutputSchemaProfileVersion version, OutputSchemaDialectId defaultDialect, ImmutableArray<OutputSchemaDialectId> supportedDialects, ImmutableArray<string> assertionKeywords, ImmutableArray<string> annotationKeywords)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id.Value, nameof(id));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version.Value, nameof(version));
        ArgumentException.ThrowIfInvalidOutputSchemaProfile(defaultDialect, supportedDialects, assertionKeywords, annotationKeywords);
        Id = id;
        Version = version;
        DefaultDialect = defaultDialect;
        SupportedDialects = [.. supportedDialects.OrderBy(static value => value.Value, StringComparer.Ordinal)];
        AssertionKeywords = [.. assertionKeywords.OrderBy(static value => value, StringComparer.Ordinal)];
        AnnotationKeywords = [.. annotationKeywords.OrderBy(static value => value, StringComparer.Ordinal)];
    }
    /// <summary>Gets the profile identity.</summary>
    public OutputSchemaProfileId Id { get; }
    /// <summary>Gets the profile revision.</summary>
    public OutputSchemaProfileVersion Version { get; }
    /// <summary>Gets the selected default dialect.</summary>
    public OutputSchemaDialectId DefaultDialect { get; }
    /// <summary>Gets supported dialects in ordinal canonical order.</summary>
    public ImmutableArray<OutputSchemaDialectId> SupportedDialects { get; }
    /// <summary>Gets supported assertion keywords in ordinal canonical order.</summary>
    public ImmutableArray<string> AssertionKeywords { get; }
    /// <summary>Gets supported annotation keywords in ordinal canonical order.</summary>
    public ImmutableArray<string> AnnotationKeywords { get; }

    /// <inheritdoc/>
    public bool Equals(OutputSchemaEngineProfile? other) =>
        other is not null
        && Id == other.Id
        && Version == other.Version
        && DefaultDialect == other.DefaultDialect
        && SupportedDialects.SequenceEqual(other.SupportedDialects)
        && AssertionKeywords.SequenceEqual(other.AssertionKeywords, StringComparer.Ordinal)
        && AnnotationKeywords.SequenceEqual(other.AnnotationKeywords, StringComparer.Ordinal);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(Version);
        hash.Add(DefaultDialect);
        foreach (var dialect in SupportedDialects)
        {
            hash.Add(dialect);
        }

        foreach (var keyword in AssertionKeywords)
        {
            hash.Add(keyword, StringComparer.Ordinal);
        }

        foreach (var keyword in AnnotationKeywords)
        {
            hash.Add(keyword, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}
