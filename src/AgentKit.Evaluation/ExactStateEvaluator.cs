// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Deterministically compares the exact final state of a run with an <see cref="ExactStateCriterion"/>.</summary>
/// <remarks>Every declared expectation must hold. Results name the expectations that failed but never record expected or produced content. The evaluator is stateless and thread-safe.</remarks>
public sealed class ExactStateEvaluator: IEvaluator
{
    /// <summary>Gets the documented evaluator key, <c>exact-state</c>.</summary>
    public static EvaluatorKey Key { get; } = new("exact-state");

    /// <inheritdoc/>
    public EvaluatorDescriptor Descriptor { get; } = new(Key, new EvaluatorVersion(1), "Exact state", [ExactStateCriterion.CriterionKey], requiresFixture: false);

    /// <inheritdoc/>
    public ValueTask<EvaluationOutcome> EvaluateAsync(EvaluationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Evaluate(context));
    }

    private static EvaluationOutcome Evaluate(EvaluationContext context)
    {
        if (context.Case.Criteria.Find<ExactStateCriterion>() is not { } criterion)
        {
            return new EvaluationUnsupported("The case declares no exact-state criterion.");
        }

        if (context.Finished is not { } finished)
        {
            return new EvaluationSkipped("The run produced no result to compare.");
        }

        var evidence = ImmutableArray.CreateBuilder<EvaluationEvidence>();
        void Check(string name, bool matched) => evidence.Add(new EvaluationEvidence(name, matched ? "matched" : "mismatch"));
        if (criterion.Outcome is { } outcome)
        {
            Check("outcome", EvaluationOutputReader.Outcome(finished.Outcome) == outcome);
        }

        if (criterion.Text is { } text)
        {
            var produced = EvaluationOutputReader.Text(context);
            Check("text", criterion.TextComparison switch
            {
                ExactTextComparison.Ordinal => string.Equals(produced, text, StringComparison.Ordinal),
                ExactTextComparison.OrdinalIgnoreCase => string.Equals(produced, text, StringComparison.OrdinalIgnoreCase),
                ExactTextComparison.TrimmedOrdinal => string.Equals(produced.Trim(), text.Trim(), StringComparison.Ordinal),
                _ => throw new InvalidOperationException("The exact text comparison is undefined."),
            });
        }

        if (criterion.Json is { } json)
        {
            Check("json", EvaluationOutputReader.TryReadJson(context, out var produced) && JsonElement.DeepEquals(json, produced));
        }

        if (criterion.NewMessageCount is { } count)
        {
            Check("new_message_count", finished.NewMessages.Length == count);
        }

        var mismatches = evidence.Count(static item => item.Value == "mismatch");
        return mismatches == 0
            ? new EvaluationPassed(EvaluationScore.Certain(true), "Every exact-state expectation matched.", evidence.ToImmutable())
            : new EvaluationFailed(EvaluationScore.Certain(false), $"{mismatches} exact-state expectation(s) did not match.", evidence.ToImmutable());
    }
}
