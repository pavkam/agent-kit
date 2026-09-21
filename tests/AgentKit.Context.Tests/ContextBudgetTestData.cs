// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Tests;

/// <summary>Valid context candidates for context package tests.</summary>
internal static class ContextBudgetTestData
{
    internal static ContextCandidate Candidate(
        int? estimatedTokens = 2,
        int priority = 0,
        bool mandatory = false) => new(
        new ContextSourceReference(
            new ContextSourceNamespace("agentkit.context"),
            new ContextSourceKey("sample"),
            new ContextSourceVersion("1")),
        ContextCandidateKind.Instruction,
        ContextTrust.AgentDefinition,
        priority,
        ContextScope.ModelRequest,
        new ContextCostEstimate(8, estimatedTokens),
        ContextFreshness.Pinned,
        ContextEvaluationFrequency.OncePerModelRequest,
        mandatory,
        [],
        ExtensionData.Empty);
}
