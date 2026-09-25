// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A compatible embedding model was selected.</summary>
public sealed record EmbeddingModelSelected(EmbeddingSelectionDecision Decision): EmbeddingSelectionResult;
