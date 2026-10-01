// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Storage;

/// <summary>Is the persisted form of an <see cref="EvaluationRunManifest"/>.</summary>
/// <param name="AgentId">The agent that ran.</param>
/// <param name="DefinitionRevision">The definition revision.</param>
/// <param name="CatalogVersion">The catalog version.</param>
/// <param name="SessionProfile">The session profile key.</param>
/// <param name="ModelCandidates">The allowed model aliases.</param>
/// <param name="ModelsUsed">The models the run used.</param>
internal sealed record EvaluationManifestDocument(
    Guid AgentId,
    long DefinitionRevision,
    long CatalogVersion,
    string SessionProfile,
    ImmutableArray<string> ModelCandidates,
    ImmutableArray<EvaluationModelUseDocument> ModelsUsed)
{
    /// <summary>Converts a manifest to its persisted form.</summary>
    /// <param name="value">The non-null manifest.</param>
    /// <returns>The document.</returns>
    internal static EvaluationManifestDocument FromDomain(EvaluationRunManifest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
            value.AgentId.Value,
            value.DefinitionRevision.Value,
            value.CatalogVersion.Value,
            value.SessionProfile.Value,
            value.ModelCandidates,
            [.. value.ModelsUsed.Select(EvaluationModelUseDocument.FromDomain)]);
    }

    /// <summary>Restores the manifest, re-running its validation.</summary>
    /// <returns>The manifest.</returns>
    internal EvaluationRunManifest ToDomain() => new(
        new AgentId(AgentId),
        new AgentDefinitionRevision(DefinitionRevision),
        new AgentCatalogVersion(CatalogVersion),
        new SessionProfileKey(SessionProfile),
        ModelCandidates.IsDefault ? [] : ModelCandidates,
        ModelsUsed.IsDefault ? [] : [.. ModelsUsed.Select(static item => item.ToDomain())]);
}
