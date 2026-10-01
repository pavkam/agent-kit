// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Holds one page of recorded results in deterministic order.</summary>
public sealed record EvaluationResultsRead: EvaluationReadResult
{
    /// <summary>Initializes a validated page.</summary>
    /// <param name="results">The results ordered by case ordinal then repetition; empty for an unknown run.</param>
    /// <param name="next">The cursor to continue after this page, or <see langword="null"/> when no more results follow.</param>
    /// <exception cref="ArgumentException"><paramref name="results"/> is default or contains null.</exception>
    public EvaluationResultsRead(ImmutableArray<EvaluationCaseResult> results, EvaluationResultCursor? next)
    {
        ArgumentException.ThrowIfDefault(results);
        ArgumentException.ThrowIfContainsNull(results);
        Results = results;
        Next = next;
    }

    /// <summary>Gets the results ordered by case ordinal then repetition.</summary>
    public ImmutableArray<EvaluationCaseResult> Results { get; }

    /// <summary>Gets the cursor to continue after this page, or <see langword="null"/> when no more results follow.</summary>
    public EvaluationResultCursor? Next { get; }

    /// <inheritdoc/>
    public bool Equals(EvaluationResultsRead? other) =>
        other is not null && Results.SequenceEqual(other.Results) && Next == other.Next;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var result in Results)
        {
            hash.Add(result);
        }

        hash.Add(Next);
        return hash.ToHashCode();
    }
}
