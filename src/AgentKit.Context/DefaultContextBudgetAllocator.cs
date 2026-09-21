// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context;

/// <summary>Reserves mandatory candidates first, then optional candidates in descending priority order.</summary>
internal sealed class DefaultContextBudgetAllocator(IContextMessageTokenEstimator estimator): IContextBudgetAllocator
{
    private readonly IContextMessageTokenEstimator _estimator = estimator;

    /// <inheritdoc/>
    public ValueTask<ContextBudgetPlan> AllocateAsync(
        ContextBudgetRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var budget = request.Budget;
        var available = budget.MaxContextTokens - budget.ReservedOutputTokens - budget.ProviderOverheadTokens;
        var effectiveAvailable = (long) Math.Floor(available * (1.0 - budget.EstimationSafetyMargin));
        if (effectiveAvailable < 0)
        {
            effectiveAvailable = 0;
        }

        var mandatory = new List<ContextCandidate>();
        var optional = new List<ContextCandidate>();
        foreach (var candidate in request.Candidates)
        {
            if (candidate.Mandatory)
            {
                mandatory.Add(candidate);
            }
            else
            {
                optional.Add(candidate);
            }
        }

        optional.Sort(static (left, right) => right.Priority.CompareTo(left.Priority));

        long mandatoryTokens = 0;
        long mandatoryBytes = 0;
        foreach (var candidate in mandatory)
        {
            mandatoryTokens += ResolveTokens(candidate);
            mandatoryBytes += candidate.Cost.Utf8Bytes;
        }

        var mandatoryOverflow = mandatoryTokens > effectiveAvailable;
        if (mandatoryOverflow && request.OverflowBehavior == ContextOverflowBehavior.Fail)
        {
            return ValueTask.FromResult(new ContextBudgetPlan(
                [],
                request.Candidates,
                new ContextCostEstimate(0, 0),
                mandatoryOverflow: true));
        }

        var selected = new List<ContextCandidate>(mandatory);
        var omitted = new List<ContextCandidate>();
        var usedTokens = mandatoryOverflow ? 0L : mandatoryTokens;
        var usedBytes = mandatoryOverflow ? 0L : mandatoryBytes;
        if (!mandatoryOverflow)
        {
            foreach (var candidate in optional)
            {
                var tokens = ResolveTokens(candidate);
                if (usedTokens + tokens <= effectiveAvailable)
                {
                    selected.Add(candidate);
                    usedTokens += tokens;
                    usedBytes += candidate.Cost.Utf8Bytes;
                }
                else
                {
                    omitted.Add(candidate);
                }
            }
        }
        else
        {
            omitted.AddRange(optional);
            omitted.AddRange(mandatory);
            selected.Clear();
        }

        return ValueTask.FromResult(new ContextBudgetPlan(
            [.. selected],
            [.. omitted],
            new ContextCostEstimate(usedBytes, (int) Math.Min(usedTokens, int.MaxValue)),
            mandatoryOverflow));
    }

    private long ResolveTokens(ContextCandidate candidate) =>
        candidate.Cost.EstimatedTokens is { } tokens
            ? tokens
            : _estimator.EstimateTokens(candidate.Content);
}
