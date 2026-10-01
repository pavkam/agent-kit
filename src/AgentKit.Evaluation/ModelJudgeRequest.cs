// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Carries one judge sample to a <see cref="IModelJudgeClient"/>.</summary>
/// <remarks>The candidate text is untrusted data. A client treats it only as content to assess and never as instructions, and the prompt already delimits it that way.</remarks>
public sealed record ModelJudgeRequest
{
    /// <summary>Initializes a validated request.</summary>
    /// <param name="context">The evaluation context of the case repetition being judged.</param>
    /// <param name="model">The explicit judge model alias.</param>
    /// <param name="instructions">The non-blank system instructions, including the recorded rubric.</param>
    /// <param name="candidate">The non-null candidate text under judgement.</param>
    /// <param name="sample">The one-based sample index within this judgement.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="candidate"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="model"/> is default or blank, or <paramref name="instructions"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sample"/> is not positive.</exception>
    public ModelJudgeRequest(EvaluationContext context, ModelAlias model, string instructions, string candidate, int sample)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(model.Value, nameof(model));
        ArgumentException.ThrowIfNullOrWhiteSpace(instructions);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sample);
        Context = context;
        Model = model;
        Instructions = instructions;
        Candidate = candidate;
        Sample = sample;
    }

    /// <summary>Gets the evaluation context of the case repetition being judged.</summary>
    public EvaluationContext Context { get; }

    /// <summary>Gets the explicit judge model alias.</summary>
    public ModelAlias Model { get; }

    /// <summary>Gets the system instructions, including the recorded rubric.</summary>
    public string Instructions { get; }

    /// <summary>Gets the candidate text under judgement.</summary>
    public string Candidate { get; }

    /// <summary>Gets the one-based sample index.</summary>
    public int Sample { get; }
}
