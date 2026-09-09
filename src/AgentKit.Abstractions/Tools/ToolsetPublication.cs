// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Publishes one immutable toolset revision with exact policy, source membership, and aliases.</summary>
/// <remarks>Publication is declarative evidence. Dynamic source versions and descriptor bindings are captured later for a run.</remarks>
public sealed record ToolsetPublication
{
    /// <summary>Creates a complete toolset publication without discovering or activating a source.</summary>
    /// <param name="key">The nondefault toolset family key.</param>
    /// <param name="version">The positive exact publication revision.</param>
    /// <param name="executionPolicy">The exact execution-policy revision selected for every member.</param>
    /// <param name="sources">The initialized, source-identity-unique authored membership in deterministic order.</param>
    /// <param name="aliases">The initialized, alias-unique explicit mappings in deterministic order.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> or <paramref name="version"/> is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="executionPolicy"/> or an array item is null.</exception>
    /// <exception cref="ArgumentException">An array is default, a source or alias is duplicated, or aliases exist without any selected source.</exception>
    public ToolsetPublication(
        ToolsetKey key,
        ToolsetVersion version,
        ToolExecutionPolicyReference executionPolicy,
        ImmutableArray<ToolsetSourceSelection> sources,
        ImmutableArray<ToolAliasAssignment> aliases)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentOutOfRangeException.ThrowIfEqual(version, default);
        ArgumentNullException.ThrowIfNull(executionPolicy);
        ArgumentException.ThrowIfContainsNull(sources);
        ArgumentException.ThrowIfContainsNull(aliases);

        var sourceIds = new HashSet<ToolSourceId>();
        foreach (var source in sources)
        {
            ArgumentException.ThrowIfNotEqual(sourceIds.Add(source.SourceId), true, nameof(sources));
        }

        var advertisedAliases = new HashSet<ToolAlias>();
        foreach (var assignment in aliases)
        {
            ArgumentException.ThrowIfNotEqual(advertisedAliases.Add(assignment.Alias), true, nameof(aliases));
        }

        if (!aliases.IsEmpty)
        {
            ArgumentException.ThrowIfNotEqual(sources.IsEmpty, false, nameof(aliases));
        }

        Key = key;
        Version = version;
        ExecutionPolicy = executionPolicy;
        Sources = sources;
        Aliases = aliases;
    }

    /// <summary>Gets the published toolset family.</summary>
    /// <value>A nondefault exact ordinal key.</value>
    public ToolsetKey Key { get; }

    /// <summary>Gets the immutable publication revision.</summary>
    /// <value>A positive exact revision.</value>
    public ToolsetVersion Version { get; }

    /// <summary>Gets the execution policy selected for published members.</summary>
    /// <value>A nonnull exact key/version reference that grants no authority.</value>
    public ToolExecutionPolicyReference ExecutionPolicy { get; }

    /// <summary>Gets authored source membership in publication order.</summary>
    /// <value>An initialized source-identity-unique array; empty is a valid empty publication.</value>
    public ImmutableArray<ToolsetSourceSelection> Sources { get; }

    /// <summary>Gets explicit provider-visible alias assignments in publication order.</summary>
    /// <value>An initialized alias-unique array; targets are proven against discovered sources during capture.</value>
    public ImmutableArray<ToolAliasAssignment> Aliases { get; }

    /// <summary>Determines complete structural publication equality.</summary>
    /// <param name="other">The publication to compare, or null.</param>
    /// <returns>True when scalar evidence and ordered membership and aliases are equal.</returns>
    public bool Equals(ToolsetPublication? other) =>
        other is not null
        && Key == other.Key
        && Version == other.Version
        && ExecutionPolicy == other.ExecutionPolicy
        && Sources.SequenceEqual(other.Sources)
        && Aliases.SequenceEqual(other.Aliases);

    /// <summary>Returns a hash compatible with complete structural equality.</summary>
    /// <returns>A hash over scalar evidence and ordered membership and aliases.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Key);
        hash.Add(Version);
        hash.Add(ExecutionPolicy);
        foreach (var source in Sources)
        {
            hash.Add(source);
        }

        foreach (var alias in Aliases)
        {
            hash.Add(alias);
        }

        return hash.ToHashCode();
    }
}
