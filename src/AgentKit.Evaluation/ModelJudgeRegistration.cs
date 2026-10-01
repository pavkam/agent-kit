// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Marks that the model judge evaluator is registered, so an identical repeat is idempotent and a different configuration is a conflict.</summary>
/// <param name="Settings">The validated settings the evaluator was registered with.</param>
internal sealed record ModelJudgeRegistration(ModelJudgeSettings Settings);
