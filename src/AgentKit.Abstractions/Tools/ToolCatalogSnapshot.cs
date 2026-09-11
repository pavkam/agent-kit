// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics;

/// <summary>Captures the exact immutable tool catalog and policy evidence bound to one run.</summary>
/// <remarks>
/// This value contains descriptive and selection evidence only. It neither resolves live invokers nor grants
/// authority. Consumers must use the captured identities and policy references at their own enforcement boundary.
/// </remarks>
public sealed record ToolCatalogSnapshot
{
    /// <summary>Initializes a run-bound immutable tool-catalog snapshot.</summary>
    /// <param name="agentId">The nondefault agent identity.</param>
    /// <param name="sessionId">The nondefault session identity.</param>
    /// <param name="runId">The nondefault active run identity.</param>
    /// <param name="identity">The authenticated identity captured for the run.</param>
    /// <param name="securityPolicy">The exact security-policy snapshot reference captured for the run.</param>
    /// <param name="agentDefinitionRevision">The nonnegative agent-definition revision.</param>
    /// <param name="configurationVersion">The positive effective-configuration version.</param>
    /// <param name="version">The nondefault immutable catalog version.</param>
    /// <param name="sourceVersions">The exact nondefault publication version of every acquired source, including sources that exposed no selected tool.</param>
    /// <param name="tools">The initialized, ordered descriptors with unique exact identities.</param>
    /// <param name="executionPolicies">The exact policy reference for every descriptor identity.</param>
    /// <param name="providerAliases">The provider-visible aliases and their exact descriptor identities.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity or required version is default.</exception>
    /// <exception cref="ArgumentNullException">A required reference or dictionary is null.</exception>
    /// <exception cref="ArgumentException">An array is uninitialized, an element is null, a descriptor identity is duplicated, dictionary normalization collides, a descriptor source is absent, the policy keyset differs from the descriptors, or an alias targets an absent descriptor.</exception>
    public ToolCatalogSnapshot(
        AgentId agentId,
        SessionId sessionId,
        RunId runId,
        ExecutionIdentity identity,
        SecurityPolicySnapshotReference securityPolicy,
        AgentDefinitionRevision agentDefinitionRevision,
        ConfigurationVersion configurationVersion,
        ToolCatalogVersion version,
        ImmutableDictionary<ToolSourceId, ToolSourceVersion> sourceVersions,
        ImmutableArray<ToolDescriptor> tools,
        ImmutableDictionary<ToolIdentity, ToolExecutionPolicyReference> executionPolicies,
        ImmutableDictionary<ToolAlias, ToolIdentity> providerAliases)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(securityPolicy);
        ArgumentOutOfRangeException.ThrowIfNegative(agentDefinitionRevision.Value, nameof(agentDefinitionRevision));
        ArgumentOutOfRangeException.ThrowIfEqual(configurationVersion, default);
        ArgumentOutOfRangeException.ThrowIfEqual(version, default);
        ArgumentNullException.ThrowIfNull(sourceVersions);
        ArgumentException.ThrowIfContainsNull(tools);
        ArgumentNullException.ThrowIfNull(executionPolicies);
        ArgumentNullException.ThrowIfNull(providerAliases);

        var capturedSources = CaptureSources(sourceVersions);
        var identities = CaptureIdentities(tools);
        foreach (var tool in tools)
        {
            ArgumentException.ThrowIfNotEqual(capturedSources.ContainsKey(tool.SourceId), true, nameof(sourceVersions));
        }
        var capturedPolicies = CapturePolicies(executionPolicies);
        var capturedAliases = CaptureAliases(providerAliases);
        ArgumentException.ThrowIfNotEqual(capturedPolicies.Count, identities.Count, nameof(executionPolicies));
        foreach (var toolIdentity in identities)
        {
            ArgumentException.ThrowIfNotEqual(capturedPolicies.ContainsKey(toolIdentity), true, nameof(executionPolicies));
        }

        foreach (var target in capturedAliases.Values)
        {
            ArgumentException.ThrowIfNotEqual(identities.Contains(target), true, nameof(providerAliases));
        }

        AgentId = agentId;
        SessionId = sessionId;
        RunId = runId;
        Identity = identity;
        SecurityPolicy = securityPolicy;
        AgentDefinitionRevision = agentDefinitionRevision;
        ConfigurationVersion = configurationVersion;
        Version = version;
        SourceVersions = capturedSources;
        Tools = tools;
        ExecutionPolicies = capturedPolicies;
        ProviderAliases = capturedAliases;
    }

    /// <summary>Gets the agent whose definition supplied this catalog.</summary>
    /// <value>The nondefault agent identity captured for the run.</value>
    public AgentId AgentId { get; }
    /// <summary>Gets the session containing the run.</summary>
    /// <value>The nondefault session identity captured for the run.</value>
    public SessionId SessionId { get; }
    /// <summary>Gets the active run bound to this snapshot.</summary>
    /// <value>The nondefault run identity; it conveys no authority by itself.</value>
    public RunId RunId { get; }
    /// <summary>Gets the authenticated execution identity captured at trusted ingress.</summary>
    /// <value>Immutable identity evidence that grants no authority.</value>
    public ExecutionIdentity Identity { get; }
    /// <summary>Gets the exact security-policy snapshot reference.</summary>
    /// <value>Captured policy evidence that consumers must resolve and revalidate.</value>
    public SecurityPolicySnapshotReference SecurityPolicy { get; }
    /// <summary>Gets the captured agent-definition revision.</summary>
    /// <value>The nonnegative revision, including zero when it is the published revision.</value>
    public AgentDefinitionRevision AgentDefinitionRevision { get; }
    /// <summary>Gets the captured effective-configuration version.</summary>
    /// <value>The positive configuration revision used for discovery.</value>
    public ConfigurationVersion ConfigurationVersion { get; }
    /// <summary>Gets the immutable catalog version.</summary>
    /// <value>An explicit nondefault version supplied by the catalog publisher.</value>
    public ToolCatalogVersion Version { get; }
    /// <summary>Gets the exact publication version pinned for each acquired source.</summary>
    /// <value>A default-comparer immutable map with a nondefault version for every descriptor source; entries for selected empty sources remain valid evidence.</value>
    public ImmutableDictionary<ToolSourceId, ToolSourceVersion> SourceVersions { get; }
    /// <summary>Gets the ordered captured descriptors.</summary>
    /// <value>An initialized immutable sequence whose exact identities are unique.</value>
    public ImmutableArray<ToolDescriptor> Tools { get; }
    /// <summary>Gets the exact execution-policy reference for every descriptor.</summary>
    /// <value>A default-comparer immutable map with exactly the descriptor identity keyset.</value>
    public ImmutableDictionary<ToolIdentity, ToolExecutionPolicyReference> ExecutionPolicies { get; }
    /// <summary>Gets provider-visible aliases mapped to exact descriptor identities.</summary>
    /// <value>A default-comparer immutable map; multiple aliases may target one identity.</value>
    public ImmutableDictionary<ToolAlias, ToolIdentity> ProviderAliases { get; }

    /// <summary>Determines whether another snapshot contains the same complete bound evidence.</summary>
    /// <param name="other">The snapshot to compare, or null.</param>
    /// <returns>True when scalar evidence matches, descriptors match in order, and all maps contain the same exact entries independent of enumeration order.</returns>
    public bool Equals(ToolCatalogSnapshot? other) =>
        other is not null &&
        AgentId == other.AgentId && SessionId == other.SessionId && RunId == other.RunId &&
        Identity == other.Identity && SecurityPolicy == other.SecurityPolicy &&
        AgentDefinitionRevision == other.AgentDefinitionRevision &&
        ConfigurationVersion == other.ConfigurationVersion && Version == other.Version &&
        Tools.SequenceEqual(other.Tools) &&
        MapsEqual(SourceVersions, other.SourceVersions) &&
        MapsEqual(ExecutionPolicies, other.ExecutionPolicies) &&
        MapsEqual(ProviderAliases, other.ProviderAliases);

    /// <summary>Returns a structural hash consistent with complete snapshot equality.</summary>
    /// <returns>A hash that preserves descriptor order and is independent of map enumeration order.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(AgentId);
        hash.Add(SessionId);
        hash.Add(RunId);
        hash.Add(Identity);
        hash.Add(SecurityPolicy);
        hash.Add(AgentDefinitionRevision);
        hash.Add(ConfigurationVersion);
        hash.Add(Version);
        hash.Add(MapHash(SourceVersions));
        hash.Add(SourceVersions.Count);
        foreach (var tool in Tools)
        {
            hash.Add(tool);
        }

        hash.Add(MapHash(ExecutionPolicies));
        hash.Add(ExecutionPolicies.Count);
        hash.Add(MapHash(ProviderAliases));
        hash.Add(ProviderAliases.Count);
        return hash.ToHashCode();
    }

    private static ImmutableDictionary<ToolSourceId, ToolSourceVersion> CaptureSources(
        ImmutableDictionary<ToolSourceId, ToolSourceVersion> sourceVersions)
    {
        Debug.Assert(sourceVersions is not null, "The public constructor rejects a null source-version map.");
        var builder = ImmutableDictionary.CreateBuilder<ToolSourceId, ToolSourceVersion>();
        foreach (var pair in sourceVersions)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(pair.Key, default, nameof(sourceVersions));
            ArgumentOutOfRangeException.ThrowIfEqual(pair.Value, default, nameof(sourceVersions));
            ArgumentException.ThrowIfNotEqual(builder.TryAdd(pair.Key, pair.Value), true, nameof(sourceVersions));
        }
        return builder.ToImmutable();
    }

    private static HashSet<ToolIdentity> CaptureIdentities(ImmutableArray<ToolDescriptor> tools)
    {
        Debug.Assert(!tools.IsDefault, "The public constructor rejects an uninitialized tools array.");
        var identities = new HashSet<ToolIdentity>();
        foreach (var tool in tools)
        {
            Debug.Assert(tool is not null, "The public constructor rejects null descriptors.");
            ArgumentException.ThrowIfNotEqual(identities.Add(new ToolIdentity(tool.Id, tool.Version)), true, nameof(tools));
        }

        return identities;
    }

    private static ImmutableDictionary<ToolIdentity, ToolExecutionPolicyReference> CapturePolicies(
        ImmutableDictionary<ToolIdentity, ToolExecutionPolicyReference> policies)
    {
        Debug.Assert(policies is not null, "The public constructor rejects a null policy map.");
        var builder = ImmutableDictionary.CreateBuilder<ToolIdentity, ToolExecutionPolicyReference>();
        foreach (var pair in policies)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(pair.Key, default, "executionPolicies");
            ArgumentNullException.ThrowIfNull(pair.Value, "executionPolicies");
            ArgumentException.ThrowIfNotEqual(builder.TryAdd(pair.Key, pair.Value), true, "executionPolicies");
        }

        return builder.ToImmutable();
    }

    private static ImmutableDictionary<ToolAlias, ToolIdentity> CaptureAliases(
        ImmutableDictionary<ToolAlias, ToolIdentity> aliases)
    {
        Debug.Assert(aliases is not null, "The public constructor rejects a null alias map.");
        var builder = ImmutableDictionary.CreateBuilder<ToolAlias, ToolIdentity>();
        foreach (var pair in aliases)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(pair.Key, default, "providerAliases");
            ArgumentOutOfRangeException.ThrowIfEqual(pair.Value, default, "providerAliases");
            ArgumentException.ThrowIfNotEqual(builder.TryAdd(pair.Key, pair.Value), true, "providerAliases");
        }

        return builder.ToImmutable();
    }

    private static bool MapsEqual<TKey, TValue>(
        ImmutableDictionary<TKey, TValue> left,
        ImmutableDictionary<TKey, TValue> right)
        where TKey : notnull
    {
        Debug.Assert(left is not null, "Snapshot-owned maps are nonnull.");
        Debug.Assert(right is not null, "Compared snapshot maps are nonnull.");
        return left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out var value) && EqualityComparer<TValue>.Default.Equals(pair.Value, value));
    }

    private static int MapHash<TKey, TValue>(ImmutableDictionary<TKey, TValue> map)
        where TKey : notnull
    {
        Debug.Assert(map is not null, "Snapshot-owned maps are nonnull.");
        var hash = 0;
        foreach (var pair in map)
        {
            hash ^= HashCode.Combine(pair.Key, pair.Value);
        }

        return hash;
    }
}
