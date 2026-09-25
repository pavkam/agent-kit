// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>No configured reranker satisfied the requirements.</summary>
public sealed record NoCompatibleReranker(
    RerankerRequirements Requirements,
    ImmutableArray<RerankerSelectionDiagnostic> Diagnostics): RerankerSelectionResult;
