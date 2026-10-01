// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Classifies how one case repetition ended, separately from what its evaluators concluded.</summary>
public enum EvaluationCaseDisposition
{
    /// <summary>The engine produced a finished run and the evaluators ran over it.</summary>
    Evaluated = 0,

    /// <summary>The engine rejected session creation or run admission, so there is no run to evaluate.</summary>
    RunRejected = 1,

    /// <summary>The caller cancelled before the repetition completed.</summary>
    Cancelled = 2,

    /// <summary>The repetition exceeded its case timeout.</summary>
    TimedOut = 3,

    /// <summary>The engine threw an unexpected exception, which is infrastructure error rather than agent quality.</summary>
    Faulted = 4,
}
