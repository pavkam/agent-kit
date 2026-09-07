// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>
/// The first-party model selector. It walks the policy's candidates in
/// declared order and returns the first one whose descriptor satisfies the
/// request's requirements.
/// </summary>
/// <remarks>
/// <para>
/// Selection is deterministic: the same policy, requirements, and catalog
/// version always produce the same decision. There is no health probing, load
/// balancing, or randomness, so a chosen model is always reproducible from the
/// recorded catalog version.
/// </para>
/// <para>
/// This implementation is stateless and thread-safe, so it is registered as a
/// singleton. It performs no provider I/O and resolves no credentials;
/// choosing a model never authorizes reaching one.
/// </para>
/// </remarks>
internal sealed partial class DefaultModelSelector(
    IModelCapabilityValidator capabilityValidator,
    ILogger<DefaultModelSelector> logger): IModelSelector
{
    private readonly IModelCapabilityValidator _capabilityValidator =
        capabilityValidator ?? throw new ArgumentNullException(nameof(capabilityValidator));

    private readonly ILogger<DefaultModelSelector> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    private async ValueTask<ModelSelectionResult> SelectCoreAsync(
        ModelSelectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var policy = request.Policy;
        var catalog = request.Catalog;
        var diagnostics = ImmutableArray.CreateBuilder<ModelSelectionDiagnostic>();

        var evaluable = policy.Fallback is ModelFallbackPolicy.OrderedCandidates
            ? policy.Candidates.Length
            : 1;

        for (var index = 0; index < policy.Candidates.Length; index++)
        {
            var alias = policy.Candidates[index];

            if (index >= evaluable)
            {
                diagnostics.Add(new ModelSelectionDiagnostic(
                    alias,
                    ModelCandidateOutcome.NotEvaluated,
                    "The selection policy permits only the first candidate."));
                continue;
            }

            var descriptor = catalog.FindConversationModel(alias);
            if (descriptor is null)
            {
                diagnostics.Add(new ModelSelectionDiagnostic(
                    alias,
                    ModelCandidateOutcome.NotInCatalog,
                    "The catalog publishes no conversational model under this alias."));
                continue;
            }

            var validation = await _capabilityValidator
                .ValidateAsync(descriptor, request.Requirements, policy.Downgrade, cancellationToken)
                .ConfigureAwait(false);

            if (validation is CapabilitiesUnsupported unsupported)
            {
                diagnostics.Add(new ModelSelectionDiagnostic(
                    alias,
                    OutcomeFor(unsupported),
                    DescribeUnsupported(unsupported)));
                continue;
            }

            diagnostics.Add(new ModelSelectionDiagnostic(
                alias,
                ModelCandidateOutcome.Selected,
                validation is CapabilitiesDowngraded downgraded
                    ? $"Selected with {downgraded.Adjustments.Length} declared capability adjustment(s)."
                    : "Selected with full capability support."));

            MarkRemainingNotEvaluated(diagnostics, policy.Candidates, index + 1);

            ProviderLog.ModelSelected(
                _logger,
                alias,
                request.ModelRequestId,
                catalog.Version.Value);

            return new ModelSelected(new ModelSelectionDecision(
                descriptor,
                catalog.Version,
                $"Candidate '{alias}' is the first configured model satisfying the request.",
                diagnostics.ToImmutable()));
        }

        ProviderLog.NoCompatibleModel(
            _logger,
            request.ModelRequestId,
            policy.Candidates.Length,
            catalog.Version.Value);

        return new NoCompatibleModel(request.Requirements, diagnostics.ToImmutable());
    }

    private static void MarkRemainingNotEvaluated(
        ImmutableArray<ModelSelectionDiagnostic>.Builder diagnostics,
        ImmutableArray<ModelAlias> candidates,
        int startIndex)
    {
        for (var index = startIndex; index < candidates.Length; index++)
        {
            diagnostics.Add(new ModelSelectionDiagnostic(
                candidates[index],
                ModelCandidateOutcome.NotEvaluated,
                "An earlier candidate was selected."));
        }
    }

    private static ModelCandidateOutcome OutcomeFor(CapabilitiesUnsupported unsupported)
    {
        // A context-window shortfall is reported distinctly because it is
        // fixed by trimming input or raising a limit, whereas a missing
        // capability requires a different model.
        foreach (var capability in unsupported.Capabilities)
        {
            if (capability.Capability is not ModelCapabilityKind.ContextWindow)
            {
                return ModelCandidateOutcome.MissingRequiredCapability;
            }
        }

        return ModelCandidateOutcome.InsufficientContextWindow;
    }

    private static string DescribeUnsupported(CapabilitiesUnsupported unsupported)
    {
        var names = unsupported.Capabilities.Select(static capability => capability.Capability.ToString());
        return $"Unsupported required capabilities: {string.Join(", ", names)}.";
    }
}
