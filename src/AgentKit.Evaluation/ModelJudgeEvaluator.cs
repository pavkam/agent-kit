// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Judges semantic quality against a <see cref="RubricCriterion"/> with a model, recording the rubric, model, prompt, and uncertainty.</summary>
/// <remarks>
/// <para>
/// Use a model judge only for criteria code cannot assess honestly; prefer the deterministic evaluators. The judge model is an
/// explicit alias with its own bounded budget. Each judgement draws <see cref="ModelJudgeSettings.RepeatCount"/> sequential
/// samples; the outcome carries the mean normalized score, its sample count, and its sample standard deviation. A judgement
/// that is incomplete (budget exhausted early), too noisy, or unparseable is inconclusive instead of a guessed pass or fail,
/// and a judge infrastructure failure is reported as an evaluator failure, not as agent quality.
/// </para>
/// <para>
/// Every result records the configured and resolved model, the provider, the full rubric, the scale, the threshold, the repeat
/// count, a SHA-256 fingerprint of the exact system prompt, and the raw sample scores. The judge reason text and the candidate
/// are never recorded. A single-candidate rubric has no candidate order, so blinded ordering does not apply; a future pairwise
/// evaluator must randomize order through <c>IRandomizerFactory</c>.
/// </para>
/// <para>The instance is thread-safe. Its budget is shared by every evaluation that uses it, so register it as a singleton.</para>
/// </remarks>
public sealed class ModelJudgeEvaluator: IEvaluator
{
    private readonly IModelJudgeClient _client;
    private readonly ModelJudgeSettings _settings;
    private readonly ModelJudgeBudgetState _budget;

    /// <summary>Gets the documented evaluator key, <c>model-judge</c>.</summary>
    public static EvaluatorKey Key { get; } = new("model-judge");

    /// <summary>Initializes the evaluator.</summary>
    /// <param name="client">The client that obtains judge replies.</param>
    /// <param name="settings">The explicit judge configuration.</param>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> or <paramref name="settings"/> is <see langword="null"/>.</exception>
    public ModelJudgeEvaluator(IModelJudgeClient client, ModelJudgeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(settings);
        _client = client;
        _settings = settings;
        _budget = new ModelJudgeBudgetState(settings.Budget);
    }

    /// <inheritdoc/>
    public EvaluatorDescriptor Descriptor { get; } = new(Key, new EvaluatorVersion(1), "Model judge", [RubricCriterion.CriterionKey], requiresFixture: false);

    /// <inheritdoc/>
    public async ValueTask<EvaluationOutcome> EvaluateAsync(EvaluationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Case.Criteria.Find<RubricCriterion>() is not { } criterion)
        {
            return new EvaluationUnsupported("The case declares no rubric criterion.");
        }

        if (context.Finished is null)
        {
            return new EvaluationSkipped("The run produced no result to judge.");
        }

        var candidate = EvaluationOutputReader.Text(context);
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return new EvaluationSkipped("The run produced no text to judge.");
        }

        var instructions = ModelJudgePrompt.Instructions(criterion);
        var evidence = ImmutableArray.CreateBuilder<EvaluationEvidence>();
        evidence.Add(new("judge.model", _settings.JudgeModel.Value));
        evidence.Add(new("judge.rubric", criterion.Rubric));
        evidence.Add(new("judge.scale", criterion.ScaleMaximum.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        evidence.Add(new("judge.threshold", criterion.PassThreshold.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
        evidence.Add(new("judge.repeat_count", _settings.RepeatCount.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        evidence.Add(new("judge.prompt.sha256", ModelJudgePrompt.Fingerprint(instructions)));
        if (candidate.Length > _settings.MaximumCandidateCharacters)
        {
            return new EvaluationInconclusive(null, "The candidate exceeds the judge length bound and was not judged.", evidence.ToImmutable());
        }

        var userMessage = ModelJudgePrompt.UserMessage(candidate);
        var scores = new List<int>();
        var served = new List<string>();
        var providers = new List<string>();
        var failures = 0;
        var invalid = 0;
        var exhausted = false;
        for (var sample = 1; sample <= _settings.RepeatCount; sample++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_budget.TryReserveCall())
            {
                exhausted = true;
                break;
            }

            var response = await _client.JudgeAsync(
                new ModelJudgeRequest(context, _settings.JudgeModel, instructions, userMessage, sample), cancellationToken).ConfigureAwait(false);
            switch (response)
            {
                case ModelJudgeCompleted completed:
                    _budget.RecordTokens(completed.InputTokens, completed.OutputTokens);
                    if (!providers.Contains(completed.Provider))
                    {
                        providers.Add(completed.Provider);
                    }

                    if (!served.Contains(completed.ResolvedModel))
                    {
                        served.Add(completed.ResolvedModel);
                    }

                    if (ModelJudgePrompt.TryParseScore(completed.Text, criterion.ScaleMaximum, out var score))
                    {
                        scores.Add(score);
                    }
                    else
                    {
                        invalid++;
                    }

                    break;
                case ModelJudgeFailed:
                    failures++;
                    break;
                default:
                    failures++;
                    break;
            }
        }

        if (providers.Count > 0)
        {
            evidence.Add(new("judge.provider", string.Join(',', providers)));
            evidence.Add(new("judge.resolved_model", string.Join(',', served)));
        }

        evidence.Add(new("judge.samples", string.Join(',', scores.Select(static score => score.ToString(System.Globalization.CultureInfo.InvariantCulture)))));
        evidence.Add(new("judge.invalid_replies", invalid.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        evidence.Add(new("judge.failed_requests", failures.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        evidence.Add(new("judge.budget.calls", _budget.Calls.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        evidence.Add(new("judge.budget.exhausted", exhausted ? "true" : "false"));
        if (scores.Count == 0)
        {
            return failures > 0 && invalid == 0 && !exhausted
                ? new EvaluatorFaulted("ModelJudgeFailure", "The judge model could not be reached for any sample.", evidence.ToImmutable())
                : new EvaluationInconclusive(
                    null,
                    exhausted ? "The judge budget was exhausted before any sample completed." : "No judge sample produced a usable score.",
                    evidence.ToImmutable());
        }

        var normalized = scores.Select(score => (double) score / criterion.ScaleMaximum).ToArray();
        var mean = normalized.Average();
        var deviation = normalized.Length > 1
            ? Math.Sqrt(normalized.Sum(value => (value - mean) * (value - mean)) / (normalized.Length - 1))
            : 0d;
        mean = Math.Clamp(mean, 0d, 1d);
        var measured = new EvaluationScore(mean, normalized.Length, deviation);
        evidence.Add(new("judge.stddev", deviation.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
        return scores.Count < _settings.RepeatCount
            ? new EvaluationInconclusive(measured, $"Only {scores.Count} of {_settings.RepeatCount} judge samples completed.", evidence.ToImmutable())
            : deviation > _settings.MaximumStandardDeviation
                ? new EvaluationInconclusive(measured, "The judge samples disagree too much to decide.", evidence.ToImmutable())
                : mean >= criterion.PassThreshold
                    ? new EvaluationPassed(measured, "The judged score meets the rubric threshold.", evidence.ToImmutable())
                    : new EvaluationFailed(measured, "The judged score is below the rubric threshold.", evidence.ToImmutable());
    }
}
