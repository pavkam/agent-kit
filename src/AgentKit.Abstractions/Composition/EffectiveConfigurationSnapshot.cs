// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics;

/// <summary>Captures one immutable, canonically ordered effective semantic configuration and its publication closure.</summary>
/// <remarks>Construction proves local ownership and source closure. The compiler separately proves setting schemas, merge legality, required paths, and cross-selection constraints.</remarks>
public sealed record EffectiveConfigurationSnapshot
{
    /// <summary>Creates one locally coherent configuration snapshot without reconstructing live sources.</summary>
    /// <param name="version">The positive publication revision.</param>
    /// <param name="fingerprint">The nondefault canonical semantic-content fingerprint.</param>
    /// <param name="entries">The initialized entries in strictly increasing ordinal path order.</param>
    /// <param name="sources">The initialized distinct source-publication closure in compiler-established order.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="fingerprint"/> is default, or an entry or source is null.</exception>
    /// <exception cref="ArgumentException">An array is uninitialized; paths are not strictly ordered; a source identity repeats; or contributor/source closure is incomplete or inconsistent.</exception>
    public EffectiveConfigurationSnapshot(
        ConfigurationVersion version,
        ContentHash fingerprint,
        ImmutableArray<EffectiveConfigurationEntry> entries,
        ImmutableArray<ConfigurationSourceReference> sources)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(version, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint.Value, nameof(fingerprint));
        ArgumentException.ThrowIfContainsNull(entries);
        ArgumentException.ThrowIfContainsNull(sources);

        ValidatePathOrder(entries);
        ValidateSourceClosure(entries, sources);

        Version = version;
        Fingerprint = fingerprint;
        Entries = entries;
        Sources = sources;
    }

    /// <summary>Gets the exact publication revision.</summary>
    /// <value>A positive configuration version retained by run publication.</value>
    public ConfigurationVersion Version { get; }

    /// <summary>Gets the canonical semantic-content fingerprint.</summary>
    /// <value>A nondefault digest independent of dictionary order, object identity, secrets, and wall-clock time.</value>
    public ContentHash Fingerprint { get; }

    /// <summary>Gets every effective semantic entry in canonical path order.</summary>
    /// <value>An initialized array with strictly increasing ordinal paths; empty is locally valid.</value>
    public ImmutableArray<EffectiveConfigurationEntry> Entries { get; }

    /// <summary>Gets the complete participating source publication closure.</summary>
    /// <value>An initialized source-identity-unique array in compiler-established precedence order.</value>
    public ImmutableArray<ConfigurationSourceReference> Sources { get; }

    /// <summary>Determines complete structural snapshot equality.</summary>
    /// <param name="other">The snapshot to compare, or null.</param>
    /// <returns>True when version, fingerprint, ordered entries, and ordered source closure are equal.</returns>
    public bool Equals(EffectiveConfigurationSnapshot? other) =>
        other is not null
        && Version == other.Version
        && Fingerprint == other.Fingerprint
        && Entries.SequenceEqual(other.Entries)
        && Sources.SequenceEqual(other.Sources);

    /// <summary>Returns a hash compatible with complete structural equality.</summary>
    /// <returns>A hash over scalar evidence and both ordered arrays.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Version);
        hash.Add(Fingerprint);
        foreach (var entry in Entries)
        {
            hash.Add(entry);
        }

        foreach (var source in Sources)
        {
            hash.Add(source);
        }

        return hash.ToHashCode();
    }

    private static void ValidatePathOrder(ImmutableArray<EffectiveConfigurationEntry> entries)
    {
        Debug.Assert(!entries.IsDefault, "The public constructor validates array initialization first.");
        for (var index = 1; index < entries.Length; index++)
        {
            var previous = entries[index - 1].Path.Value;
            var current = entries[index].Path.Value;
            Debug.Assert(previous is not null && current is not null, "Entry construction validates path text.");
            ArgumentException.ThrowIfNotEqual(
                StringComparer.Ordinal.Compare(previous, current) < 0,
                true,
                nameof(entries));
        }
    }

    private static void ValidateSourceClosure(
        ImmutableArray<EffectiveConfigurationEntry> entries,
        ImmutableArray<ConfigurationSourceReference> sources)
    {
        Debug.Assert(!entries.IsDefault && !sources.IsDefault, "The public constructor validates both arrays first.");
        var publications = new Dictionary<ConfigurationSourceId, ConfigurationSourceReference>();
        foreach (var source in sources)
        {
            ArgumentException.ThrowIfNotEqual(publications.TryAdd(source.SourceId, source), true, nameof(sources));
        }

        var participating = new HashSet<ConfigurationSourceId>();
        foreach (var entry in entries)
        {
            foreach (var contributor in entry.Contributors)
            {
                ArgumentException.ThrowIfNotEqual(
                    publications.TryGetValue(contributor.SourceId, out var publication) && publication == contributor,
                    true,
                    nameof(entries));
                _ = participating.Add(contributor.SourceId);
            }
        }

        ArgumentException.ThrowIfNotEqual(participating.Count, publications.Count, nameof(sources));
    }
}
