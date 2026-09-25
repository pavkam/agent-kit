// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>No configured candidate satisfied the embedding requirements.</summary>
public sealed record NoCompatibleEmbeddingModel(
    EmbeddingRequirements Requirements,
    ImmutableArray<EmbeddingSelectionDiagnostic> Diagnostics): EmbeddingSelectionResult;
