// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Validates selected publications and constructs the complete ordered collision graph without external effects.</summary>
/// <remarks>Sources are already discovered exactly once. This graph borrows immutable snapshots and owns no provider capture or invoker.</remarks>
internal sealed class ToolCatalogMergeGraph
{
    /// <summary>Validates exact toolset/source membership before any policy or diagnostic callback.</summary>
    /// <param name="request">The nonnull coherent discovery request selecting the toolsets.</param>
    /// <param name="toolsets">Initialized nonnull publications in the request's authored order, with matching keys and policy families.</param>
    /// <param name="sources">The nonnull complete source map, including selected empty sources, with exact typed keys.</param>
    /// <exception cref="ArgumentNullException">A required reference or a source-map value is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A source key is default.</exception>
    /// <exception cref="ArgumentException">An array is default or contains null, toolsets differ from the request, source-key normalization collides, a source declares a different key, or the selected source keyset differs.</exception>
    internal ToolCatalogMergeGraph(ToolDiscoveryRequest request, ImmutableArray<ToolsetPublication> toolsets,
        ImmutableDictionary<ToolSourceId, ToolProviderSnapshot> sources)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfContainsNull(toolsets);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentException.ThrowIfNotEqual(toolsets.Length, request.Toolsets.Length, nameof(toolsets));
        for (var index = 0; index < toolsets.Length; index++)
        {
            ArgumentException.ThrowIfNotEqual(toolsets[index].Key, request.Toolsets[index].Key, nameof(toolsets));
            ArgumentException.ThrowIfNotEqual(toolsets[index].ExecutionPolicy.Key, request.Toolsets[index].ExecutionPolicyKey, nameof(toolsets));
        }
        var captured = ImmutableDictionary.CreateBuilder<ToolSourceId, ToolProviderSnapshot>();
        foreach (var pair in sources)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(pair.Key, default, nameof(sources));
            ArgumentNullException.ThrowIfNull(pair.Value, nameof(sources));
            ArgumentException.ThrowIfNotEqual(pair.Key, pair.Value.SourceId, nameof(sources));
            ArgumentException.ThrowIfNotEqual(captured.TryAdd(pair.Key, pair.Value), true, nameof(sources));
        }
        var selectedSources = toolsets.SelectMany(static toolset => toolset.Sources).Select(static selection => selection.SourceId).ToHashSet();
        ArgumentException.ThrowIfNotEqual(selectedSources.SetEquals(captured.Keys), true, nameof(sources));

        var candidates = ImmutableArray.CreateBuilder<ToolCatalogCandidate>();
        foreach (var toolset in toolsets)
        {
            foreach (var selection in toolset.Sources)
            {
                var source = captured[selection.SourceId];
                foreach (var tool in source.Tools)
                {
                    candidates.Add(new ToolCatalogCandidate(toolset, source, new ToolIdentity(tool.Id, tool.Version)));
                }
            }
        }
        var contributions = candidates.ToImmutable();
        var collisions = ImmutableArray.CreateBuilder<ToolCatalogCollision>();
        foreach (var group in contributions.GroupBy(static candidate => candidate.Identity))
        {
            if (group.Count() > 1) { collisions.Add(new ToolCatalogIdentityCollision([.. group])); }
        }
        var aliasCandidates = new Dictionary<ToolAlias, List<ToolCatalogCandidate>>();
        var aliasOrder = new List<ToolAlias>();
        var missing = new List<ToolCatalogMissingAliasTarget>();
        foreach (var toolset in toolsets)
        {
            foreach (var assignment in toolset.Aliases)
            {
                if (!aliasCandidates.TryGetValue(assignment.Alias, out var targets))
                {
                    targets = [];
                    aliasCandidates.Add(assignment.Alias, targets);
                    aliasOrder.Add(assignment.Alias);
                }
                var matches = contributions.Where(candidate => candidate.Toolset.Key == toolset.Key && candidate.Identity == assignment.Tool).ToArray();
                targets.AddRange(matches);
                if (matches.Length == 0) { missing.Add(new ToolCatalogMissingAliasTarget(toolset, assignment)); }
            }
        }
        foreach (var alias in aliasOrder)
        {
            var missingTargets = missing.Where(target => target.Assignment.Alias == alias).ToImmutableArray();
            if ((long) aliasCandidates[alias].Count + missingTargets.Length > 1)
            {
                collisions.Add(new ToolCatalogAliasCollision(alias, [.. aliasCandidates[alias]], missingTargets));
            }
        }
        collisions.AddRange(missing);
        Sources = captured.ToImmutable();
        Aliases = [.. aliasOrder];
        Context = new ToolCatalogMergeContext(request, contributions, collisions.ToImmutable());
    }

    /// <summary>Gets every exact discovered source, including empty publications.</summary>
    /// <value>A normalized map with exactly the selected source identities.</value>
    internal ImmutableDictionary<ToolSourceId, ToolProviderSnapshot> Sources { get; }

    /// <summary>Gets every distinct authored alias in first-authored order.</summary>
    /// <value>Includes missing targets, so a policy cannot hide them by dropping an assignment.</value>
    internal ImmutableArray<ToolAlias> Aliases { get; }

    /// <summary>Gets the complete policy input constructed from validated publications.</summary>
    /// <value>Immutable candidates and deterministic collision evidence.</value>
    internal ToolCatalogMergeContext Context { get; }

    /// <summary>Revalidates a complete policy selection and constructs the run-bound snapshot.</summary>
    /// <param name="selection">The nonnull proposed choice returned by policy.</param>
    /// <param name="version">The nondefault catalog version assigned by the catalog owner.</param>
    /// <returns>A snapshot containing only original descriptor/source/policy/alias evidence, ordered by first identity contribution.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selection"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is default.</exception>
    /// <exception cref="InvalidOperationException">Policy invents evidence, omits a required contribution, disagrees with its selected binding, or attempts to repair a missing target.</exception>
    internal ToolCatalogSnapshot Apply(ToolCatalogSelection selection, ToolCatalogVersion version)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentOutOfRangeException.ThrowIfEqual(version, default);
        var available = Context.Candidates.ToHashSet();
        var groups = Context.Candidates.GroupBy(static candidate => candidate.Identity).ToArray();
        var selected = selection.Tools.ToDictionary(static candidate => candidate.Identity);
        if (Context.Collisions.Any(static collision => collision is ToolCatalogMissingAliasTarget)
            || selected.Count != groups.Length
            || selection.Tools.Any(candidate => !available.Contains(candidate))
            || groups.Any(group => !selected.ContainsKey(group.Key))
            || selection.Aliases.Count != Aliases.Length
            || Aliases.Any(alias => !selection.Aliases.ContainsKey(alias)))
        {
            throw new InvalidOperationException("Catalog policy must select the complete existing contribution graph.");
        }
        foreach (var candidate in selection.Aliases.Values)
        {
            if (!available.Contains(candidate)
                || !selected.TryGetValue(candidate.Identity, out var chosen)
                || candidate.Source != chosen.Source
                || candidate.Tool != chosen.Tool
                || candidate.Toolset.ExecutionPolicy != chosen.Toolset.ExecutionPolicy)
            {
                throw new InvalidOperationException("Catalog alias selection must agree with its selected source, descriptor, and policy.");
            }
        }
        var request = Context.Request;
        return new ToolCatalogSnapshot(request.AgentId, request.SessionId, request.RunId, request.Identity,
            request.Authorization.PolicySnapshot, request.AgentDefinitionRevision, request.Configuration.Version, version,
            Sources.ToImmutableDictionary(static pair => pair.Key, static pair => pair.Value.SourceVersion),
            [.. groups.Select(group => selected[group.Key].Tool)],
            selected.ToImmutableDictionary(static pair => pair.Key, static pair => pair.Value.Toolset.ExecutionPolicy),
            selection.Aliases.ToImmutableDictionary(static pair => pair.Key, static pair => pair.Value.Identity));
    }
}
