// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Carries the collaborators the runtime selector resolved for one profile activation.</summary>
/// <param name="MemoryStore">The memory store, or <see langword="null"/>.</param>
/// <param name="DocumentStore">The document store, or <see langword="null"/>.</param>
/// <param name="VectorIndexes">The vector indexes in declared order.</param>
/// <param name="Sources">The retrieval sources in declared order.</param>
/// <param name="QueryRewriter">The captured rewriter, or <see langword="null"/>.</param>
/// <param name="EmbeddingSelector">The embedding selector, or <see langword="null"/>.</param>
/// <param name="EmbeddingExecutor">The embedding executor, or <see langword="null"/>.</param>
/// <param name="RerankerSelector">The reranker selector, or <see langword="null"/>.</param>
/// <param name="RerankerExecutor">The reranker executor, or <see langword="null"/>.</param>
/// <param name="Models">The model catalog snapshot captured at activation.</param>
/// <param name="SecurityAuthorities">The security authority selector.</param>
/// <param name="Budget">The operation-owned budget capability.</param>
/// <param name="BudgetPolicy">The retrieval budget policy.</param>
/// <param name="Events">The event dispatcher.</param>
internal sealed record MemoryRuntimeCollaborators(
    IMemoryStore? MemoryStore,
    IDocumentStore? DocumentStore,
    ImmutableArray<IVectorIndex> VectorIndexes,
    ImmutableArray<IRetrievalSource> Sources,
    IQueryRewriter? QueryRewriter,
    IEmbeddingModelSelector? EmbeddingSelector,
    IEmbeddingRequestExecutor? EmbeddingExecutor,
    IRerankerSelector? RerankerSelector,
    IRerankRequestExecutor? RerankerExecutor,
    ModelCatalogSnapshot Models,
    ISecurityAuthoritySelector SecurityAuthorities,
    BudgetExecutionCapability Budget,
    IRetrievalBudgetPolicy BudgetPolicy,
    IMemoryEventDispatcher Events);
