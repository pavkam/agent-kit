// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Classifies how one evaluation run ended.</summary>
public enum EvaluationReportStatus
{
    /// <summary>Every scheduled case repetition ran to a recorded result.</summary>
    Completed = 0,

    /// <summary>The caller cancelled; the report holds the results recorded so far.</summary>
    Cancelled = 1,

    /// <summary>The plan deadline elapsed; the report holds the results recorded so far.</summary>
    DeadlineExceeded = 2,

    /// <summary>An evaluator failure stopped scheduling because the plan asked for that.</summary>
    StoppedOnEvaluatorFailure = 3,
}
