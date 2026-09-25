// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Rerank execution completed successfully.</summary>
public sealed record RerankExecutionSucceeded(
    RerankerSelectionDecision Selection,
    int Attempts,
    RerankResponse Response): RerankExecutionResult;
