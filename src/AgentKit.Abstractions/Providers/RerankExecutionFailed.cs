// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Rerank execution failed without a fallback path.</summary>
public sealed record RerankExecutionFailed(
    RerankerSelectionDecision Selection,
    int Attempts,
    ProviderFailure Failure): RerankExecutionResult;
