// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Summarizes the evaluators of one case repetition into one honest verdict.</summary>
public enum EvaluationVerdict
{
    /// <summary>The repetition was evaluated, no evaluator failed, and at least one passed.</summary>
    Passed = 0,

    /// <summary>At least one evaluator reported a failed outcome.</summary>
    Failed = 1,

    /// <summary>No evaluator failed, but at least one was inconclusive, cancelled, unsupported, or faulted.</summary>
    Inconclusive = 2,

    /// <summary>The repetition was not evaluated, or no evaluator concluded.</summary>
    NotEvaluated = 3,
}
