// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Owns the exact keyed collaborators activated for one memory profile for the duration of one operation.</summary>
/// <remarks>
/// The lease borrows container-owned singletons and owns the activation scope that resolved scoped collaborators. Disposal
/// releases that scope only; it never cancels work handed off to a store or undoes a committed effect, and it never disposes a
/// borrowed host instance. Disposal is idempotent and must not race the operation that borrowed the collaborators.
/// </remarks>
internal sealed class MemoryProfileRuntimeLease: IMemoryProfileRuntimeLease
{
    private readonly IServiceScope _scope;
    private bool _disposed;

    /// <summary>Captures one activated runtime and the scope that owns its scoped collaborators.</summary>
    /// <param name="scope">The activation scope released on disposal.</param>
    /// <param name="profile">The immutable profile the runtime was activated for.</param>
    /// <param name="collaborators">The resolved collaborators.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    internal MemoryProfileRuntimeLease(IServiceScope scope, MemoryProfileSnapshot profile, MemoryRuntimeCollaborators collaborators)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(collaborators);
        _scope = scope;
        Profile = profile;
        MemoryStore = collaborators.MemoryStore;
        DocumentStore = collaborators.DocumentStore;
        VectorIndexes = collaborators.VectorIndexes;
        Sources = new CapturedRetrievalSourceSelector(collaborators.Sources);
        QueryRewriter = collaborators.QueryRewriter;
        EmbeddingSelector = collaborators.EmbeddingSelector;
        EmbeddingExecutor = collaborators.EmbeddingExecutor;
        RerankerSelector = collaborators.RerankerSelector;
        RerankerExecutor = collaborators.RerankerExecutor;
        Models = collaborators.Models;
        SecurityAuthorities = collaborators.SecurityAuthorities;
        Budget = collaborators.Budget;
        BudgetPolicy = collaborators.BudgetPolicy;
        Events = collaborators.Events;
    }

    /// <inheritdoc/>
    public MemoryProfileSnapshot Profile { get; }

    /// <inheritdoc/>
    public IMemoryStore? MemoryStore { get; }

    /// <inheritdoc/>
    public IDocumentStore? DocumentStore { get; }

    /// <inheritdoc/>
    public ImmutableArray<IVectorIndex> VectorIndexes { get; }

    /// <inheritdoc/>
    public IRetrievalSourceSelector Sources { get; }

    /// <inheritdoc/>
    public IQueryRewriter? QueryRewriter { get; }

    /// <inheritdoc/>
    public IEmbeddingModelSelector? EmbeddingSelector { get; }

    /// <inheritdoc/>
    public IEmbeddingRequestExecutor? EmbeddingExecutor { get; }

    /// <inheritdoc/>
    public IRerankerSelector? RerankerSelector { get; }

    /// <inheritdoc/>
    public IRerankRequestExecutor? RerankerExecutor { get; }

    /// <inheritdoc/>
    public ModelCatalogSnapshot Models { get; }

    /// <inheritdoc/>
    public ISecurityAuthoritySelector SecurityAuthorities { get; }

    /// <inheritdoc/>
    public BudgetExecutionCapability Budget { get; }

    /// <inheritdoc/>
    public IRetrievalBudgetPolicy BudgetPolicy { get; }

    /// <inheritdoc/>
    public IMemoryEventDispatcher Events { get; }

    /// <inheritdoc/>
    /// <remarks>Repeated disposal is harmless. Releasing the scope never cancels handed-off store work or undoes a committed effect.</remarks>
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        if (_scope is IAsyncDisposable asyncScope)
        {
            return asyncScope.DisposeAsync();
        }

        _scope.Dispose();
        return ValueTask.CompletedTask;
    }
}
