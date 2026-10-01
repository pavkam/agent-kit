// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Carries the immutable evidence one evaluator may inspect about one case repetition.</summary>
/// <remarks>Everything here is public engine output or authored dataset data: the typed run result, the case, and the run manifest. The runner supplies no mutable runtime state, and the context never grants authority.</remarks>
public sealed record EvaluationContext
{
    /// <summary>Initializes a validated context.</summary>
    /// <param name="runId">The evaluation run this repetition belongs to.</param>
    /// <param name="planId">The plan being executed.</param>
    /// <param name="planVersion">The plan version being executed.</param>
    /// <param name="evaluationCase">The case being evaluated.</param>
    /// <param name="repetition">The one-based repetition of the case.</param>
    /// <param name="result">The public result of the case run.</param>
    /// <param name="manifest">The recorded agent and model manifest.</param>
    /// <param name="usage">The recorded usage summary.</param>
    /// <param name="latency">The non-negative elapsed run time.</param>
    /// <exception cref="ArgumentNullException">A required reference is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default, or <paramref name="repetition"/> is not positive, or <paramref name="latency"/> is negative.</exception>
    public EvaluationContext(
        EvaluationRunId runId,
        EvaluationPlanId planId,
        EvaluationPlanVersion planVersion,
        EvaluationCase evaluationCase,
        int repetition,
        AgentRunResult<ValidatedOutput> result,
        EvaluationRunManifest manifest,
        EvaluationUsageSummary usage,
        TimeSpan latency)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default, nameof(runId));
        ArgumentException.ThrowIfNullOrWhiteSpace(planId.Value, nameof(planId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(planVersion.Value, nameof(planVersion));
        ArgumentNullException.ThrowIfNull(evaluationCase);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(repetition);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentOutOfRangeException.ThrowIfLessThan(latency, TimeSpan.Zero);
        RunId = runId;
        PlanId = planId;
        PlanVersion = planVersion;
        Case = evaluationCase;
        Repetition = repetition;
        Result = result;
        Manifest = manifest;
        Usage = usage;
        Latency = latency;
    }

    /// <summary>Gets the evaluation run this repetition belongs to.</summary>
    public EvaluationRunId RunId { get; }

    /// <summary>Gets the plan being executed.</summary>
    public EvaluationPlanId PlanId { get; }

    /// <summary>Gets the plan version being executed.</summary>
    public EvaluationPlanVersion PlanVersion { get; }

    /// <summary>Gets the case being evaluated.</summary>
    public EvaluationCase Case { get; }

    /// <summary>Gets the one-based repetition of the case.</summary>
    public int Repetition { get; }

    /// <summary>Gets the public result of the case run.</summary>
    public AgentRunResult<ValidatedOutput> Result { get; }

    /// <summary>Gets the recorded agent and model manifest.</summary>
    public EvaluationRunManifest Manifest { get; }

    /// <summary>Gets the recorded usage summary.</summary>
    public EvaluationUsageSummary Usage { get; }

    /// <summary>Gets the elapsed run time.</summary>
    public TimeSpan Latency { get; }

    /// <summary>Gets the finished run, or <see langword="null"/> when the engine rejected it.</summary>
    public AgentRunFinished<ValidatedOutput>? Finished => Result as AgentRunFinished<ValidatedOutput>;

    /// <summary>Gets the concatenated text of the assistant messages the run produced.</summary>
    /// <value>The text in message order, or an empty string when the run was rejected or produced no assistant text.</value>
    public string AssistantText
    {
        get
        {
            if (Finished is not { } finished)
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder();
            foreach (var message in finished.NewMessages)
            {
                if (message is not AssistantMessage)
                {
                    continue;
                }

                foreach (var part in message.Parts)
                {
                    if (part is TextPart text)
                    {
                        _ = builder.Append(text.Text);
                    }
                }
            }

            return builder.ToString();
        }
    }
}
