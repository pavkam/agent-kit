// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Collections.Immutable;

/// <summary>Builds reduced run-bound <see cref="ToolCatalogSnapshot"/> evidence from the legacy singleton catalog.</summary>
internal static class LegacyToolCatalogSnapshotFactory
{
    private static readonly ToolExecutionPolicyReference _defaultExecutionPolicy = new(
        new ToolExecutionPolicyKey("legacy-default"),
        new ToolExecutionPolicyVersion(1));

    private static readonly ToolCatalogVersion _legacyCatalogVersion = new("legacy-catalog");

    /// <summary>Captures the registered legacy catalog as immutable run-bound evidence.</summary>
    /// <param name="catalog">The nonnull legacy catalog whose descriptors are copied.</param>
    /// <param name="agentId">The owning agent.</param>
    /// <param name="sessionId">The owning session.</param>
    /// <param name="runId">The active run.</param>
    /// <param name="authorization">The captured authorization for the run.</param>
    /// <returns>A validated snapshot suitable for <see cref="LegacyToolCatalogCapture"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> or <paramref name="authorization"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default.</exception>
    internal static ToolCatalogSnapshot Create(
        IToolCatalog catalog,
        AgentId agentId,
        SessionId sessionId,
        RunId runId,
        SecurityAuthorizationContext authorization)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);

        var tools = catalog.Descriptors;
        var sourceVersionsBuilder = ImmutableDictionary.CreateBuilder<ToolSourceId, ToolSourceVersion>();
        var policiesBuilder = ImmutableDictionary.CreateBuilder<ToolIdentity, ToolExecutionPolicyReference>();
        var aliasesBuilder = ImmutableDictionary.CreateBuilder<ToolAlias, ToolIdentity>();

        foreach (var descriptor in tools)
        {
            var identity = new ToolIdentity(descriptor.Id, descriptor.Version);
            _ = policiesBuilder.TryAdd(identity, _defaultExecutionPolicy);
            _ = aliasesBuilder.TryAdd(new ToolAlias(descriptor.Id.Value), identity);
            _ = sourceVersionsBuilder.TryAdd(descriptor.SourceId, new ToolSourceVersion("legacy"));
        }

        return new ToolCatalogSnapshot(
            agentId,
            sessionId,
            runId,
            authorization.Identity,
            authorization.PolicySnapshot,
            authorization.AgentDefinitionRevision,
            authorization.ConfigurationVersion,
            _legacyCatalogVersion,
            sourceVersionsBuilder.ToImmutable(),
            tools,
            policiesBuilder.ToImmutable(),
            aliasesBuilder.ToImmutable());
    }

    /// <summary>Gets the catalog version stamped on legacy adapter snapshots.</summary>
    internal static ToolCatalogVersion LegacyCatalogVersion { get; } = _legacyCatalogVersion;
}
