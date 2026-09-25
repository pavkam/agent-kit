// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>The first-party embedding model selector.</summary>
internal sealed class DefaultEmbeddingModelSelector: IEmbeddingModelSelector
{
    /// <inheritdoc/>
    public ValueTask<EmbeddingSelectionResult> SelectAsync(
        EmbeddingSelectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var policy = request.Policy;
        var catalog = request.Catalog;
        var diagnostics = ImmutableArray.CreateBuilder<EmbeddingSelectionDiagnostic>();
        var evaluable = policy.Fallback is SemanticFallbackPolicy.OrderedCandidates
            ? policy.Candidates.Length
            : 1;

        for (var index = 0; index < policy.Candidates.Length; index++)
        {
            var alias = policy.Candidates[index];
            if (index >= evaluable)
            {
                diagnostics.Add(new EmbeddingSelectionDiagnostic(
                    alias,
                    ModelCandidateOutcome.NotEvaluated,
                    "The selection policy permits only the first candidate."));
                continue;
            }

            var descriptor = catalog.FindEmbeddingModel(alias);
            if (descriptor is null)
            {
                diagnostics.Add(new EmbeddingSelectionDiagnostic(
                    alias,
                    ModelCandidateOutcome.NotInCatalog,
                    "The catalog publishes no embedding model under this alias."));
                continue;
            }

            if (!SatisfiesRequirements(descriptor, request.Requirements, out var reason))
            {
                diagnostics.Add(new EmbeddingSelectionDiagnostic(
                    alias,
                    ModelCandidateOutcome.MissingRequiredCapability,
                    reason));
                continue;
            }

            return ValueTask.FromResult<EmbeddingSelectionResult>(new EmbeddingModelSelected(
                new EmbeddingSelectionDecision(
                    descriptor,
                    $"Candidate '{alias}' is the first configured embedding model satisfying the request.",
                    catalog.Version)));
        }

        return ValueTask.FromResult<EmbeddingSelectionResult>(
            new NoCompatibleEmbeddingModel(request.Requirements, diagnostics.ToImmutable()));
    }

    private static bool SatisfiesRequirements(
        EmbeddingModelDescriptor descriptor,
        EmbeddingRequirements requirements,
        out string reason)
    {
        var capabilities = descriptor.Capabilities;
        if (requirements.RequiresBatchInput && !capabilities.SupportsBatchInput)
        {
            reason = "The model does not support batched input.";
            return false;
        }

        if (requirements.RequiresDimensions && !capabilities.SupportsDimensions)
        {
            reason = "The model does not support reduced dimensionality.";
            return false;
        }

        if (requirements.RequiresPurpose && !capabilities.SupportsPurpose)
        {
            reason = "The model does not support purpose-specific transforms.";
            return false;
        }

        if (requirements.RequiresEncodingSelection && !capabilities.SupportsEncodingSelection)
        {
            reason = "The model does not support encoding selection.";
            return false;
        }

        if (requirements.RequiresTruncationControl && !capabilities.SupportsTruncationControl)
        {
            reason = "The model does not support truncation control.";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
