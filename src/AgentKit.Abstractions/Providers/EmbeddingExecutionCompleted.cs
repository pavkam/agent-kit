// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Embedding execution completed successfully.</summary>
public sealed record EmbeddingExecutionCompleted(
    EmbeddingSelectionDecision Selection,
    int Attempts,
    EmbeddingResponse Response): EmbeddingExecutionResult;
