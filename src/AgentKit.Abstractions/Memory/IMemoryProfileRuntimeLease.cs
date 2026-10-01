// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Owns the exact services selected for one memory profile for the duration of one operation.</summary>
/// <remarks>
/// <para>
/// The lease keeps ordinary constructor injection useful while avoiding keyed-service discovery inside the pipeline: a caller
/// can never combine one agent's source selector or rewriter with another agent's embedding model, security authority, budgets,
/// stores, or event dispatcher. Members for axes the profile does not enable are <see langword="null"/> or empty, and using one
/// returns a typed unavailable result before any I/O.
/// </para>
/// <para>Disposal releases the activation scope only; it never cancels work handed off to a store or undoes a committed effect. Disposal is idempotent.</para>
/// </remarks>
public interface IMemoryProfileRuntimeLease: IAsyncDisposable
{
    /// <summary>Gets the immutable profile this lease was activated for.</summary>
    public MemoryProfileSnapshot Profile { get; }

    /// <summary>Gets the selected durable-memory store, or <see langword="null"/> when the profile has none.</summary>
    public IMemoryStore? MemoryStore { get; }

    /// <summary>Gets the selected document store, or <see langword="null"/> when the profile has none.</summary>
    public IDocumentStore? DocumentStore { get; }

    /// <summary>Gets the selected vector indexes in the profile's declared order.</summary>
    public ImmutableArray<IVectorIndex> VectorIndexes { get; }

    /// <summary>Gets the selector over the profile's captured retrieval sources.</summary>
    public IRetrievalSourceSelector Sources { get; }

    /// <summary>Gets the captured query rewriter, or <see langword="null"/> when rewriting is disabled.</summary>
    public IQueryRewriter? QueryRewriter { get; }

    /// <summary>Gets the selected embedding model selector, or <see langword="null"/> when embedding is not enabled.</summary>
    public IEmbeddingModelSelector? EmbeddingSelector { get; }

    /// <summary>Gets the selected embedding request executor, or <see langword="null"/> when embedding is not enabled.</summary>
    public IEmbeddingRequestExecutor? EmbeddingExecutor { get; }

    /// <summary>Gets the selected reranker selector, or <see langword="null"/> when reranking is not enabled.</summary>
    public IRerankerSelector? RerankerSelector { get; }

    /// <summary>Gets the selected rerank request executor, or <see langword="null"/> when reranking is not enabled.</summary>
    public IRerankRequestExecutor? RerankerExecutor { get; }

    /// <summary>Gets the one immutable model catalog snapshot captured at activation.</summary>
    public ModelCatalogSnapshot Models { get; }

    /// <summary>Gets the selector that resolves the security authority a captured authorization names.</summary>
    public ISecurityAuthoritySelector SecurityAuthorities { get; }

    /// <summary>Gets the operation-owned budget capability covering retrieval and any embedding or reranking child attempts.</summary>
    public BudgetExecutionCapability Budget { get; }

    /// <summary>Gets the policy that enforces the item, byte, and token bounds.</summary>
    public IRetrievalBudgetPolicy BudgetPolicy { get; }

    /// <summary>Gets the dispatcher that delivers immutable memory events for this profile.</summary>
    public IMemoryEventDispatcher Events { get; }
}
