// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Classifies one reason a plan cannot run against the composition.</summary>
public enum EvaluationPlanProblemKind
{
    /// <summary>The plan asks for more concurrency, repetition, or case runs than the runner options allow.</summary>
    ExceedsLimits = 0,

    /// <summary>A case names an agent the engine does not host.</summary>
    AgentNotFound = 1,

    /// <summary>The resolved agent definition does not select the session profile the case declares.</summary>
    SessionProfileMismatch = 2,

    /// <summary>A case references an evaluator that is not registered, or at a version other than the pinned one.</summary>
    EvaluatorUnavailable = 3,

    /// <summary>An evaluator cannot assess a case and the plan does not permit unsupported evaluators.</summary>
    EvaluatorUnsupported = 4,

    /// <summary>The plan names a result store or exporter that is not registered.</summary>
    DestinationUnavailable = 5,
}
