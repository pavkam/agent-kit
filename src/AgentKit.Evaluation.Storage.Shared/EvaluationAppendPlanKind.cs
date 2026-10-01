// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Storage;

/// <summary>Classifies the planner decision for one append.</summary>
internal enum EvaluationAppendPlanKind
{
    /// <summary>The result is new and must be persisted.</summary>
    Applied = 0,

    /// <summary>An identical result is already recorded; nothing is written.</summary>
    Replayed = 1,

    /// <summary>The result conflicts with recorded evidence and is refused.</summary>
    Rejected = 2,
}
