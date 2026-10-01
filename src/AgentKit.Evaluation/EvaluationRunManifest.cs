// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Records the exact agent definition, session profile, and models one case repetition ran under.</summary>
/// <remarks>The manifest is built from the public agent catalog and the run usage evidence, never from internal runtime state, so a custom engine implementation records the same shape.</remarks>
public sealed record EvaluationRunManifest
{
    /// <summary>Initializes a validated manifest.</summary>
    /// <param name="agentId">The agent that ran.</param>
    /// <param name="definitionRevision">The definition revision the run used.</param>
    /// <param name="catalogVersion">The catalog version that published the definition.</param>
    /// <param name="sessionProfile">The session profile the definition selects.</param>
    /// <param name="modelCandidates">The model aliases the definition allows, in preference order.</param>
    /// <param name="modelsUsed">The distinct models the run reported using, in first-use order; empty when none was reported.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="agentId"/> is default or a version is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="sessionProfile"/> is blank, or an array is default or contains a blank or null item.</exception>
    public EvaluationRunManifest(
        AgentId agentId,
        AgentDefinitionRevision definitionRevision,
        AgentCatalogVersion catalogVersion,
        SessionProfileKey sessionProfile,
        ImmutableArray<string> modelCandidates,
        ImmutableArray<EvaluationModelUse> modelsUsed)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default, nameof(agentId));
        ArgumentOutOfRangeException.ThrowIfNegative(definitionRevision.Value, nameof(definitionRevision));
        ArgumentOutOfRangeException.ThrowIfNegative(catalogVersion.Value, nameof(catalogVersion));
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionProfile.Value, nameof(sessionProfile));
        ArgumentException.ThrowIfDefault(modelCandidates);
        foreach (var candidate in modelCandidates)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(candidate, nameof(modelCandidates));
        }

        ArgumentException.ThrowIfDefault(modelsUsed);
        ArgumentException.ThrowIfContainsNull(modelsUsed);
        AgentId = agentId;
        DefinitionRevision = definitionRevision;
        CatalogVersion = catalogVersion;
        SessionProfile = sessionProfile;
        ModelCandidates = modelCandidates;
        ModelsUsed = modelsUsed;
    }

    /// <summary>Gets the agent that ran.</summary>
    public AgentId AgentId { get; }

    /// <summary>Gets the definition revision the run used.</summary>
    public AgentDefinitionRevision DefinitionRevision { get; }

    /// <summary>Gets the catalog version that published the definition.</summary>
    public AgentCatalogVersion CatalogVersion { get; }

    /// <summary>Gets the session profile the definition selects.</summary>
    public SessionProfileKey SessionProfile { get; }

    /// <summary>Gets the model aliases the definition allows, in preference order.</summary>
    public ImmutableArray<string> ModelCandidates { get; }

    /// <summary>Gets the distinct models the run reported using.</summary>
    public ImmutableArray<EvaluationModelUse> ModelsUsed { get; }

    /// <inheritdoc/>
    public bool Equals(EvaluationRunManifest? other) =>
        other is not null
        && AgentId == other.AgentId
        && DefinitionRevision == other.DefinitionRevision
        && CatalogVersion == other.CatalogVersion
        && SessionProfile == other.SessionProfile
        && ModelCandidates.SequenceEqual(other.ModelCandidates)
        && ModelsUsed.SequenceEqual(other.ModelsUsed);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(AgentId);
        hash.Add(DefinitionRevision);
        hash.Add(CatalogVersion);
        hash.Add(SessionProfile);
        foreach (var candidate in ModelCandidates)
        {
            hash.Add(candidate, StringComparer.Ordinal);
        }

        foreach (var model in ModelsUsed)
        {
            hash.Add(model);
        }

        return hash.ToHashCode();
    }
}
