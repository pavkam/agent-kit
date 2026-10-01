// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Keeps the longest ranked prefix of candidates that fits the item, byte, and token bounds.</summary>
/// <remarks>The policy only removes candidates from the tail. It never reorders, skips past an oversized candidate to admit a lower-ranked one, or truncates text, so provenance and rank stay exact. Tokens are estimated as UTF-8 bytes divided by the configured bytes per token, rounded up.</remarks>
/// <param name="options">The engine-wide options carrying the bytes-per-token estimate.</param>
internal sealed class BoundedRetrievalBudgetPolicy(IOptions<AgentMemoryOptions> options): IRetrievalBudgetPolicy
{
    /// <inheritdoc/>
    public ValueTask<RetrievalBudgetDecision> SelectAsync(RetrievalBudgetRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var bytesPerToken = options.Value.EstimatedBytesPerToken;
        var budget = request.Budget;
        var selected = ImmutableArray.CreateBuilder<RetrievalCandidate>();
        long bytes = 0;
        long tokens = 0;
        foreach (var candidate in request.Candidates)
        {
            var candidateBytes = candidate.Content.Utf8Bytes;
            var candidateTokens = (long) Math.Ceiling(candidateBytes / bytesPerToken);
            if (selected.Count >= budget.MaximumItems || bytes + candidateBytes > budget.MaximumBytes || tokens + candidateTokens > budget.MaximumTokens)
            {
                break;
            }

            selected.Add(candidate);
            bytes += candidateBytes;
            tokens += candidateTokens;
        }

        return ValueTask.FromResult(new RetrievalBudgetDecision(selected.ToImmutable(), request.Candidates.Length - selected.Count));
    }
}
