// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Configures the finite concurrency, repetition, and timeout mechanics of the first-party evaluation runner.</summary>
/// <remarks>
/// These are caps and defaults, not plan data. The runner captures one immutable <c>EvaluationOptionsSnapshot</c> when it is
/// first resolved and never reads this mutable object again while an evaluation is active. Identity, session profile,
/// credentials, agent definitions, and external destinations stay explicit plan or keyed composition data.
/// </remarks>
public sealed class EvaluationOptions
{
    /// <summary>Gets or sets the most case repetitions a plan may run at once.</summary>
    /// <value>A positive cap. The default is 4. A plan that asks for more is rejected before any case runs.</value>
    public int MaximumConcurrentCases { get; set; } = 4;

    /// <summary>Gets or sets the most repetitions a plan may request for each case.</summary>
    /// <value>A positive cap. The default is 1. A plan that asks for more is rejected before any case runs.</value>
    public int MaximumRepetitions { get; set; } = 1;

    /// <summary>Gets or sets the per-repetition time limit used when a plan declares none, and the bound on each recording write.</summary>
    /// <value>A positive duration. The default is five minutes.</value>
    public TimeSpan DefaultCaseTimeout { get; set; } = TimeSpan.FromMinutes(5);
}
