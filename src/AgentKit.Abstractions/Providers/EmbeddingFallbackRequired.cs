// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Embedding execution requires reselection to another candidate.</summary>
public sealed record EmbeddingFallbackRequired(
    EmbeddingSelectionDecision Selection,
    int Attempts,
    ProviderFailure Failure): EmbeddingExecutionResult;
