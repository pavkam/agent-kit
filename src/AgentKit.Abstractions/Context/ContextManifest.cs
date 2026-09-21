// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The immutable provenance manifest for one assembled model request.</summary>
public sealed record ContextManifest
{
    /// <summary>Initializes a complete assembly manifest.</summary>
    /// <param name="modelRequestId">The model request this manifest describes.</param>
    /// <param name="agentDefinitionRevision">The agent definition revision used during assembly.</param>
    /// <param name="sessionVersion">The session version observed while assembling history.</param>
    /// <param name="configurationVersion">The effective configuration version used during assembly.</param>
    /// <param name="contributorCatalogVersion">The contributor catalog revision used during assembly.</param>
    /// <param name="model">The selected model descriptor.</param>
    /// <param name="entries">The ordered manifest entries for included, transformed, and omitted sources.</param>
    /// <param name="totalEstimate">The aggregate estimated cost of projected content.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Any required identity or version is default.</exception>
    /// <exception cref="ArgumentException"><paramref name="entries"/> is a default, uninitialized array.</exception>
    public ContextManifest(
        ModelRequestId modelRequestId,
        AgentDefinitionRevision agentDefinitionRevision,
        SessionVersion sessionVersion,
        ConfigurationVersion configurationVersion,
        ContextContributorCatalogVersion contributorCatalogVersion,
        ModelDescriptor model,
        ImmutableArray<ContextManifestEntry> entries,
        ContextCostEstimate totalEstimate)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(modelRequestId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(agentDefinitionRevision, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionVersion, default);
        ArgumentOutOfRangeException.ThrowIfEqual(configurationVersion, default);
        ArgumentOutOfRangeException.ThrowIfEqual(contributorCatalogVersion, default);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfDefault(entries);

        ModelRequestId = modelRequestId;
        AgentDefinitionRevision = agentDefinitionRevision;
        SessionVersion = sessionVersion;
        ConfigurationVersion = configurationVersion;
        ContributorCatalogVersion = contributorCatalogVersion;
        Model = model;
        Entries = entries;
        TotalEstimate = totalEstimate;
    }

    /// <summary>Gets the model request this manifest describes.</summary>
    public ModelRequestId ModelRequestId { get; }

    /// <summary>Gets the agent definition revision used during assembly.</summary>
    public AgentDefinitionRevision AgentDefinitionRevision { get; }

    /// <summary>Gets the session version observed while assembling history.</summary>
    public SessionVersion SessionVersion { get; }

    /// <summary>Gets the effective configuration version used during assembly.</summary>
    public ConfigurationVersion ConfigurationVersion { get; }

    /// <summary>Gets the contributor catalog revision used during assembly.</summary>
    public ContextContributorCatalogVersion ContributorCatalogVersion { get; }

    /// <summary>Gets the selected model descriptor.</summary>
    public ModelDescriptor Model { get; }

    /// <summary>Gets the ordered manifest entries for included, transformed, and omitted sources.</summary>
    public ImmutableArray<ContextManifestEntry> Entries { get; }

    /// <summary>Gets the aggregate estimated cost of projected content.</summary>
    public ContextCostEstimate TotalEstimate { get; }
}
