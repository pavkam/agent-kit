// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records one effective semantic setting and every source publication that participated in its merge.</summary>
public sealed record EffectiveConfigurationEntry
{
    /// <summary>Creates one locally complete effective entry without claiming setting-schema conformance.</summary>
    /// <param name="path">The nondefault namespace-qualified semantic setting path.</param>
    /// <param name="mergeOperation">The defined merge operation that produced the value.</param>
    /// <param name="value">The nonnull owned document or typed selection value.</param>
    /// <param name="contributors">The initialized, nonempty, source-identity-unique publications in merge order.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="path"/> is default or <paramref name="mergeOperation"/> is undefined.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or a contributor is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="contributors"/> is default, empty, or repeats a source identity.</exception>
    public EffectiveConfigurationEntry(
        ConfigurationPath path,
        ConfigurationMergeOperation mergeOperation,
        ConfigurationSemanticValue value,
        ImmutableArray<ConfigurationSourceReference> contributors)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(path, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(mergeOperation);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfContainsNull(contributors);
        ArgumentException.ThrowIfDefaultOrEmpty(contributors);

        var sourceIds = new HashSet<ConfigurationSourceId>();
        foreach (var contributor in contributors)
        {
            ArgumentException.ThrowIfNotEqual(sourceIds.Add(contributor.SourceId), true, nameof(contributors));
        }

        Path = path;
        MergeOperation = mergeOperation;
        Value = value;
        Contributors = contributors;
    }

    /// <summary>Gets the semantic setting identity.</summary>
    /// <value>A nondefault namespace-qualified path whose grammar the compiler validates.</value>
    public ConfigurationPath Path { get; }

    /// <summary>Gets the merge operation that produced the value.</summary>
    /// <value>A defined operation retained as effective-configuration provenance.</value>
    public ConfigurationMergeOperation MergeOperation { get; }

    /// <summary>Gets the owned effective semantic value.</summary>
    /// <value>A nonnull member of the closed document-and-selection family.</value>
    public ConfigurationSemanticValue Value { get; }

    /// <summary>Gets every participating source publication in merge order.</summary>
    /// <value>An initialized nonempty array unique by source identity, including overridden and reset contributors.</value>
    public ImmutableArray<ConfigurationSourceReference> Contributors { get; }

    /// <summary>Determines complete structural entry equality.</summary>
    /// <param name="other">The entry to compare, or null.</param>
    /// <returns>True when path, merge operation, semantic value, and ordered contributors are equal.</returns>
    public bool Equals(EffectiveConfigurationEntry? other) =>
        other is not null
        && Path == other.Path
        && MergeOperation == other.MergeOperation
        && Value == other.Value
        && Contributors.SequenceEqual(other.Contributors);

    /// <summary>Returns a hash compatible with complete structural equality.</summary>
    /// <returns>A hash over scalar evidence and ordered contributors.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Path);
        hash.Add(MergeOperation);
        hash.Add(Value);
        foreach (var contributor in Contributors)
        {
            hash.Add(contributor);
        }

        return hash.ToHashCode();
    }
}
