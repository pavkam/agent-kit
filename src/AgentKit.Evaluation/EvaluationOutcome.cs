// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Is the typed terminal result of one evaluator over one case repetition.</summary>
/// <remarks>
/// The outcome discriminates passed, failed, inconclusive, skipped by declared precondition, cancelled, unsupported, and
/// evaluator failure so quality, refusal, cancellation, and infrastructure error are never collapsed into one boolean. Every
/// variant carries a safe, content-free summary that is bounded and never contains prompts, model output, or tool data, plus
/// optional named evidence the evaluator chose to record.
/// </remarks>
public abstract record EvaluationOutcome
{
    private protected EvaluationOutcome(string name, string summary, EvaluationScore? score, ImmutableArray<EvaluationEvidence> evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentException.ThrowIfContainsNull(evidence.IsDefault ? [] : evidence, nameof(evidence));
        Name = name;
        Summary = summary;
        Score = score;
        Evidence = evidence.IsDefault ? [] : evidence;
    }

    /// <summary>Gets the bounded stable variant name used by metrics, logs, and persisted results.</summary>
    /// <value>One of <c>passed</c>, <c>failed</c>, <c>inconclusive</c>, <c>skipped</c>, <c>cancelled</c>, <c>unsupported</c>, or <c>evaluator_failed</c>.</value>
    public string Name { get; }

    /// <summary>Gets the safe, content-free explanation.</summary>
    public string Summary { get; }

    /// <summary>Gets the score and its uncertainty, or <see langword="null"/> when the variant carries none.</summary>
    public EvaluationScore? Score { get; }

    /// <summary>Gets the safe named facts the evaluator recorded about how it reached the outcome.</summary>
    /// <value>The facts in recorded order; empty when the evaluator recorded none.</value>
    public ImmutableArray<EvaluationEvidence> Evidence { get; }

    /// <inheritdoc/>
    public virtual bool Equals(EvaluationOutcome? other) =>
        other is not null
        && GetType() == other.GetType()
        && string.Equals(Name, other.Name, StringComparison.Ordinal)
        && string.Equals(Summary, other.Summary, StringComparison.Ordinal)
        && Equals(Score, other.Score)
        && Evidence.SequenceEqual(other.Evidence);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(GetType());
        hash.Add(Name, StringComparer.Ordinal);
        hash.Add(Summary, StringComparer.Ordinal);
        hash.Add(Score);
        foreach (var item in Evidence)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }
}
