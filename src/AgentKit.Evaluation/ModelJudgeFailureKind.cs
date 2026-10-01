// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Classifies why a judge client could not produce a judgement.</summary>
public enum ModelJudgeFailureKind
{
    /// <summary>The judge model or its provider is currently unavailable or failed the request.</summary>
    Unavailable = 0,

    /// <summary>The composition names no model that can serve the judge request.</summary>
    NotConfigured = 1,

    /// <summary>The model stopped before completing a reply, for example at its output limit or by requesting a tool.</summary>
    Incomplete = 2,
}
