// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Bundles the immutable collaborators every case repetition of one run shares.</summary>
internal sealed record EvaluationCaseRunContext(
    AgentEngine Engine,
    EvaluationRunId RunId,
    EvaluationPlan Plan,
    ValidatedEvaluationPlan Validated,
    TimeProvider Time,
    TimeSpan CaseTimeout,
    TimeSpan RecordingTimeout,
    ILogger Logger);
