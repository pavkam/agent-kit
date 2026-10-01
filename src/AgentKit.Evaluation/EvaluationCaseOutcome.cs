// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Carries one finished case repetition back to the scheduler.</summary>
/// <param name="Result">The recorded result.</param>
/// <param name="Store">What the result store answered, or <see langword="null"/> when no store was selected.</param>
/// <param name="EvaluatorFaulted">Whether any evaluator threw, which can stop later scheduling.</param>
internal sealed record EvaluationCaseOutcome(
    EvaluationCaseResult Result,
    EvaluationStoreAppendRecord? Store,
    bool EvaluatorFaulted);
