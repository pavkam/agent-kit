// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Storage;

/// <summary>Records the plan identity and version the first result of a run pinned it to.</summary>
/// <param name="PlanId">The plan identity.</param>
/// <param name="PlanVersion">The plan version.</param>
internal sealed record EvaluationRunPin(EvaluationPlanId PlanId, EvaluationPlanVersion PlanVersion);
