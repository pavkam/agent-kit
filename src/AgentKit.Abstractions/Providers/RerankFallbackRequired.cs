// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Rerank execution requires reselection to another candidate.</summary>
public sealed record RerankFallbackRequired(
    RerankerSelectionDecision Selection,
    int Attempts,
    ProviderFailure Failure): RerankExecutionResult;
