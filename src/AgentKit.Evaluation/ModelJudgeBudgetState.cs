// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Accounts the spend of one <see cref="ModelJudgeBudget"/> atomically across concurrent evaluations.</summary>
internal sealed class ModelJudgeBudgetState
{
    private readonly ModelJudgeBudget _budget;
    private int _calls;
    private long _tokens;

    /// <summary>Initializes empty accounting over a budget.</summary>
    /// <param name="budget">The non-null budget.</param>
    internal ModelJudgeBudgetState(ModelJudgeBudget budget)
    {
        Debug.Assert(budget is not null, "Settings supply a budget.");
        _budget = budget;
    }

    /// <summary>Gets the number of requests reserved so far.</summary>
    internal int Calls => Volatile.Read(ref _calls);

    /// <summary>Gets the number of tokens recorded so far.</summary>
    internal long Tokens => Interlocked.Read(ref _tokens);

    /// <summary>Reserves one request if neither bound is exhausted.</summary>
    /// <returns><see langword="true"/> when the caller may issue one request; the reservation is never returned.</returns>
    internal bool TryReserveCall()
    {
        while (true)
        {
            if (_budget.MaximumTokens is { } maximumTokens && Tokens >= maximumTokens)
            {
                return false;
            }

            var current = Volatile.Read(ref _calls);
            if (current >= _budget.MaximumCalls)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _calls, current + 1, current) == current)
            {
                return true;
            }
        }
    }

    /// <summary>Records tokens a finished request reported.</summary>
    /// <param name="inputTokens">The reported input tokens, or <see langword="null"/>.</param>
    /// <param name="outputTokens">The reported output tokens, or <see langword="null"/>.</param>
    internal void RecordTokens(long? inputTokens, long? outputTokens) =>
        _ = Interlocked.Add(ref _tokens, (inputTokens ?? 0) + (outputTokens ?? 0));
}
