// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>The first-party reranker selector.</summary>
internal sealed class DefaultRerankerSelector: IRerankerSelector
{
    /// <inheritdoc/>
    public ValueTask<RerankerSelectionResult> SelectAsync(
        RerankerSelectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var policy = request.Policy;
        var catalog = request.Catalog;
        var diagnostics = ImmutableArray.CreateBuilder<RerankerSelectionDiagnostic>();
        var evaluable = policy.Fallback is SemanticFallbackPolicy.OrderedCandidates
            ? policy.Candidates.Length
            : 1;

        for (var index = 0; index < policy.Candidates.Length; index++)
        {
            var alias = policy.Candidates[index];
            if (index >= evaluable)
            {
                diagnostics.Add(new RerankerSelectionDiagnostic(
                    alias,
                    ModelCandidateOutcome.NotEvaluated,
                    "The selection policy permits only the first candidate."));
                continue;
            }

            var descriptor = catalog.FindReranker(alias);
            if (descriptor is null)
            {
                diagnostics.Add(new RerankerSelectionDiagnostic(
                    alias,
                    ModelCandidateOutcome.NotInCatalog,
                    "The catalog publishes no reranker under this alias."));
                continue;
            }

            if (request.Requirements.RequiresTopCount && !descriptor.Capabilities.SupportsTopCount)
            {
                diagnostics.Add(new RerankerSelectionDiagnostic(
                    alias,
                    ModelCandidateOutcome.MissingRequiredCapability,
                    "The reranker does not support top-count limits."));
                continue;
            }

            return ValueTask.FromResult<RerankerSelectionResult>(new RerankerSelected(
                new RerankerSelectionDecision(
                    descriptor,
                    $"Candidate '{alias}' is the first configured reranker satisfying the request.",
                    catalog.Version)));
        }

        return ValueTask.FromResult<RerankerSelectionResult>(
            new NoCompatibleReranker(request.Requirements, diagnostics.ToImmutable()));
    }
}
