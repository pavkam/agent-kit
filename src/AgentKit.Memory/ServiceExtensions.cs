// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Registers the AgentKit memory and retrieval runtime and its replaceable collaborators.</summary>
/// <remarks>
/// <para>Every method returns the same <see cref="IServiceCollection"/>, never builds a service provider, and is idempotent for an identical registration. Adding a different implementation under an already-registered key or identity throws <see cref="InvalidOperationException"/>; the matching <c>Replace</c> method changes it deliberately.</para>
/// <para>The runtime registers no concrete store, vector index, embedding model, reranker, or retrieval source by default. Applications select each explicitly, and a profile names exactly what it uses.</para>
/// </remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the memory coordinator, retrieval pipeline, profile runtime selector, policy and event dispatchers, and default collaborators.</summary>
        /// <param name="configure">An optional callback that adjusts the engine-wide <see cref="AgentMemoryOptions"/>; successive calls compose.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException">The service collection is null.</exception>
        /// <remarks>Options are validated when the host starts. Repeating the call only applies the additional configuration and never duplicates a registration.</remarks>
        public IServiceCollection AddAgentMemory(Action<AgentMemoryOptions>? configure = null) => MemoryServiceRegistration.AddAgentMemory(services, configure);

        /// <summary>Declares a memory profile that agent definitions select by key.</summary>
        /// <param name="key">The non-empty profile key.</param>
        /// <param name="configure">The callback that configures the profile's stores, sources, policy profile, and ceilings; successive declarations under one key compose.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException">The collection or callback is null.</exception>
        /// <exception cref="ArgumentException">The key is uninitialized or blank.</exception>
        public IServiceCollection AddMemoryProfile(MemoryProfileKey key, Action<MemoryProfileOptions> configure) =>
            MemoryServiceRegistration.AddMemoryProfile(services, key, configure, replace: false);

        /// <summary>Declares a memory profile that replaces earlier declarations under the same key.</summary>
        /// <param name="key">The non-empty profile key.</param>
        /// <param name="configure">The callback that configures the replacement profile.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException">The collection or callback is null.</exception>
        /// <exception cref="ArgumentException">The key is uninitialized or blank.</exception>
        public IServiceCollection ReplaceMemoryProfile(MemoryProfileKey key, Action<MemoryProfileOptions> configure) =>
            MemoryServiceRegistration.AddMemoryProfile(services, key, configure, replace: true);

        /// <summary>Registers a durable-memory store under a stable key.</summary>
        /// <typeparam name="TStore">The store implementation.</typeparam>
        /// <param name="key">The non-empty store key.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentException">The key is uninitialized or blank.</exception>
        /// <exception cref="InvalidOperationException">A different store is already registered under the key.</exception>
        public IServiceCollection AddMemoryStore<TStore>(MemoryStoreKey key)
            where TStore : class, IMemoryStore => MemoryServiceRegistration.AddKeyed<IMemoryStore, TStore>(services, key.Value, nameof(key), replace: false);

        /// <summary>Registers a durable-memory store under a stable key, replacing any earlier registration of that key.</summary>
        /// <typeparam name="TStore">The store implementation.</typeparam>
        /// <param name="key">The non-empty store key.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentException">The key is uninitialized or blank.</exception>
        public IServiceCollection ReplaceMemoryStore<TStore>(MemoryStoreKey key)
            where TStore : class, IMemoryStore => MemoryServiceRegistration.AddKeyed<IMemoryStore, TStore>(services, key.Value, nameof(key), replace: true);

        /// <summary>Registers a document store under a stable key.</summary>
        /// <typeparam name="TStore">The store implementation.</typeparam>
        /// <param name="key">The non-empty store key.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentException">The key is uninitialized or blank.</exception>
        /// <exception cref="InvalidOperationException">A different store is already registered under the key.</exception>
        public IServiceCollection AddDocumentStore<TStore>(DocumentStoreKey key)
            where TStore : class, IDocumentStore => MemoryServiceRegistration.AddKeyed<IDocumentStore, TStore>(services, key.Value, nameof(key), replace: false);

        /// <summary>Registers a document store under a stable key, replacing any earlier registration of that key.</summary>
        /// <typeparam name="TStore">The store implementation.</typeparam>
        /// <param name="key">The non-empty store key.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentException">The key is uninitialized or blank.</exception>
        public IServiceCollection ReplaceDocumentStore<TStore>(DocumentStoreKey key)
            where TStore : class, IDocumentStore => MemoryServiceRegistration.AddKeyed<IDocumentStore, TStore>(services, key.Value, nameof(key), replace: true);

        /// <summary>Registers a vector index under a stable key.</summary>
        /// <typeparam name="TIndex">The index implementation.</typeparam>
        /// <param name="key">The non-empty index key.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentException">The key is uninitialized or blank.</exception>
        /// <exception cref="InvalidOperationException">A different index is already registered under the key.</exception>
        public IServiceCollection AddVectorIndex<TIndex>(VectorIndexKey key)
            where TIndex : class, IVectorIndex => MemoryServiceRegistration.AddKeyed<IVectorIndex, TIndex>(services, key.Value, nameof(key), replace: false);

        /// <summary>Registers a vector index under a stable key, replacing any earlier registration of that key.</summary>
        /// <typeparam name="TIndex">The index implementation.</typeparam>
        /// <param name="key">The non-empty index key.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentException">The key is uninitialized or blank.</exception>
        public IServiceCollection ReplaceVectorIndex<TIndex>(VectorIndexKey key)
            where TIndex : class, IVectorIndex => MemoryServiceRegistration.AddKeyed<IVectorIndex, TIndex>(services, key.Value, nameof(key), replace: true);

        /// <summary>Registers a retrieval source under its descriptor key.</summary>
        /// <typeparam name="TSource">The source implementation.</typeparam>
        /// <param name="key">The non-empty source key a profile selects.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentException">The key is uninitialized or blank.</exception>
        /// <exception cref="InvalidOperationException">A different source is already registered under the key.</exception>
        public IServiceCollection AddRetrievalSource<TSource>(RetrievalSourceKey key)
            where TSource : class, IRetrievalSource => MemoryServiceRegistration.AddKeyed<IRetrievalSource, TSource>(services, key.Value, nameof(key), replace: false);

        /// <summary>Registers a retrieval source under its descriptor key, replacing any earlier registration of that key.</summary>
        /// <typeparam name="TSource">The source implementation.</typeparam>
        /// <param name="key">The non-empty source key a profile selects.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentException">The key is uninitialized or blank.</exception>
        public IServiceCollection ReplaceRetrievalSource<TSource>(RetrievalSourceKey key)
            where TSource : class, IRetrievalSource => MemoryServiceRegistration.AddKeyed<IRetrievalSource, TSource>(services, key.Value, nameof(key), replace: true);

        /// <summary>Registers the first-party durable-memory keyword source under <see cref="MemoryRetrievalSourceKeys.DurableMemory"/>.</summary>
        /// <returns>The same service collection.</returns>
        /// <remarks>Registering the source does not select it; a profile must name the key.</remarks>
        public IServiceCollection AddDurableMemoryRetrievalSource() => MemoryServiceRegistration.AddDurableMemorySource(services);

        /// <summary>Registers the first-party document-chunk vector source under <see cref="MemoryRetrievalSourceKeys.Documents"/>.</summary>
        /// <returns>The same service collection.</returns>
        /// <remarks>Registering the source does not select it; a profile must name the key and the vector indexes it searches.</remarks>
        public IServiceCollection AddDocumentRetrievalSource() => MemoryServiceRegistration.AddDocumentSource(services);

        /// <summary>Registers a query rewriter under its descriptor key.</summary>
        /// <typeparam name="TRewriter">The rewriter implementation.</typeparam>
        /// <param name="descriptor">The rewriter's key and version.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException">The descriptor is null.</exception>
        /// <exception cref="InvalidOperationException">A different rewriter is already registered under the key.</exception>
        public IServiceCollection AddQueryRewriter<TRewriter>(QueryRewriterDescriptor descriptor)
            where TRewriter : class, IQueryRewriter => MemoryServiceRegistration.AddQueryRewriter<TRewriter>(services, descriptor, replace: false);

        /// <summary>Registers a query rewriter under its descriptor key, replacing any earlier registration of that key.</summary>
        /// <typeparam name="TRewriter">The rewriter implementation.</typeparam>
        /// <param name="descriptor">The rewriter's key and version.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException">The descriptor is null.</exception>
        public IServiceCollection ReplaceQueryRewriter<TRewriter>(QueryRewriterDescriptor descriptor)
            where TRewriter : class, IQueryRewriter => MemoryServiceRegistration.AddQueryRewriter<TRewriter>(services, descriptor, replace: true);

        /// <summary>Registers an embedding selector under a stable component key a profile names.</summary>
        /// <typeparam name="TSelector">The selector implementation.</typeparam>
        /// <param name="key">The non-empty component key.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentException">The key is uninitialized or blank.</exception>
        /// <exception cref="InvalidOperationException">A different selector is already registered under the key.</exception>
        public IServiceCollection AddMemoryEmbeddingSelector<TSelector>(ComponentKey<IEmbeddingModelSelector> key)
            where TSelector : class, IEmbeddingModelSelector => MemoryServiceRegistration.AddKeyed<IEmbeddingModelSelector, TSelector>(services, key.Value, nameof(key), replace: false);

        /// <summary>Registers an embedding request executor under a stable component key a profile names.</summary>
        /// <typeparam name="TExecutor">The executor implementation.</typeparam>
        /// <param name="key">The non-empty component key.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentException">The key is uninitialized or blank.</exception>
        /// <exception cref="InvalidOperationException">A different executor is already registered under the key.</exception>
        public IServiceCollection AddMemoryEmbeddingExecutor<TExecutor>(ComponentKey<IEmbeddingRequestExecutor> key)
            where TExecutor : class, IEmbeddingRequestExecutor => MemoryServiceRegistration.AddKeyed<IEmbeddingRequestExecutor, TExecutor>(services, key.Value, nameof(key), replace: false);

        /// <summary>Registers a reranker selector under a stable component key a profile names.</summary>
        /// <typeparam name="TSelector">The selector implementation.</typeparam>
        /// <param name="key">The non-empty component key.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentException">The key is uninitialized or blank.</exception>
        /// <exception cref="InvalidOperationException">A different selector is already registered under the key.</exception>
        public IServiceCollection AddMemoryRerankerSelector<TSelector>(ComponentKey<IRerankerSelector> key)
            where TSelector : class, IRerankerSelector => MemoryServiceRegistration.AddKeyed<IRerankerSelector, TSelector>(services, key.Value, nameof(key), replace: false);

        /// <summary>Registers a rerank request executor under a stable component key a profile names.</summary>
        /// <typeparam name="TExecutor">The executor implementation.</typeparam>
        /// <param name="key">The non-empty component key.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentException">The key is uninitialized or blank.</exception>
        /// <exception cref="InvalidOperationException">A different executor is already registered under the key.</exception>
        public IServiceCollection AddMemoryRerankerExecutor<TExecutor>(ComponentKey<IRerankRequestExecutor> key)
            where TExecutor : class, IRerankRequestExecutor => MemoryServiceRegistration.AddKeyed<IRerankRequestExecutor, TExecutor>(services, key.Value, nameof(key), replace: false);

        /// <summary>Registers a memory policy that participates in the named policy profile.</summary>
        /// <typeparam name="TPolicy">The policy implementation.</typeparam>
        /// <param name="registration">The policy profile, identity, order, and lifetime.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException">The registration is null.</exception>
        /// <exception cref="InvalidOperationException">A different policy is already registered under the identity.</exception>
        public IServiceCollection AddMemoryPolicy<TPolicy>(MemoryPolicyRegistration registration)
            where TPolicy : class, IMemoryPolicy => MemoryServiceRegistration.AddPolicy<TPolicy>(services, registration, replace: false);

        /// <summary>Registers a memory policy, replacing any earlier registration with the same identity.</summary>
        /// <typeparam name="TPolicy">The policy implementation.</typeparam>
        /// <param name="registration">The policy profile, identity, order, and lifetime.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException">The registration is null.</exception>
        public IServiceCollection ReplaceMemoryPolicy<TPolicy>(MemoryPolicyRegistration registration)
            where TPolicy : class, IMemoryPolicy => MemoryServiceRegistration.AddPolicy<TPolicy>(services, registration, replace: true);

        /// <summary>Registers a memory event sink.</summary>
        /// <typeparam name="TSink">The sink implementation.</typeparam>
        /// <param name="registration">The sink identity, order, delivery guarantee, lifetime, and observed profiles.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException">The registration is null.</exception>
        /// <exception cref="InvalidOperationException">A different sink is already registered under the identity.</exception>
        public IServiceCollection AddMemoryEventSink<TSink>(MemoryEventSinkRegistration registration)
            where TSink : class, IMemoryEventSink => MemoryServiceRegistration.AddEventSink<TSink>(services, registration);

        /// <summary>Replaces the singular document chunker.</summary>
        /// <typeparam name="TChunker">The chunker implementation.</typeparam>
        /// <returns>The same service collection.</returns>
        public IServiceCollection ReplaceDocumentChunker<TChunker>()
            where TChunker : class, IDocumentChunker => MemoryServiceRegistration.ReplaceSingular<IDocumentChunker, TChunker>(services);

        /// <summary>Replaces the singular memory coordinator.</summary>
        /// <typeparam name="TCoordinator">The coordinator implementation.</typeparam>
        /// <returns>The same service collection.</returns>
        public IServiceCollection ReplaceMemoryCoordinator<TCoordinator>()
            where TCoordinator : class, IMemoryCoordinator => MemoryServiceRegistration.ReplaceSingular<IMemoryCoordinator, TCoordinator>(services);

        /// <summary>Replaces the singular document lifecycle coordinator.</summary>
        /// <typeparam name="TCoordinator">The coordinator implementation.</typeparam>
        /// <returns>The same service collection.</returns>
        public IServiceCollection ReplaceDocumentLifecycleCoordinator<TCoordinator>()
            where TCoordinator : class, IDocumentLifecycleCoordinator => MemoryServiceRegistration.ReplaceSingular<IDocumentLifecycleCoordinator, TCoordinator>(services);

        /// <summary>Replaces the singular retrieval pipeline.</summary>
        /// <typeparam name="TPipeline">The pipeline implementation.</typeparam>
        /// <returns>The same service collection.</returns>
        public IServiceCollection ReplaceRetrievalPipeline<TPipeline>()
            where TPipeline : class, IRetrievalPipeline => MemoryServiceRegistration.ReplaceSingular<IRetrievalPipeline, TPipeline>(services);

        /// <summary>Replaces the singular retrieval budget policy.</summary>
        /// <typeparam name="TPolicy">The budget policy implementation.</typeparam>
        /// <returns>The same service collection.</returns>
        public IServiceCollection ReplaceRetrievalBudgetPolicy<TPolicy>()
            where TPolicy : class, IRetrievalBudgetPolicy => MemoryServiceRegistration.ReplaceSingular<IRetrievalBudgetPolicy, TPolicy>(services);

        /// <summary>Replaces the singular memory policy dispatcher.</summary>
        /// <typeparam name="TDispatcher">The dispatcher implementation.</typeparam>
        /// <returns>The same service collection.</returns>
        public IServiceCollection ReplaceMemoryPolicyDispatcher<TDispatcher>()
            where TDispatcher : class, IMemoryPolicyDispatcher => MemoryServiceRegistration.ReplaceSingular<IMemoryPolicyDispatcher, TDispatcher>(services);

        /// <summary>Replaces the singular memory event dispatcher.</summary>
        /// <typeparam name="TDispatcher">The dispatcher implementation.</typeparam>
        /// <returns>The same service collection.</returns>
        public IServiceCollection ReplaceMemoryEventDispatcher<TDispatcher>()
            where TDispatcher : class, IMemoryEventDispatcher => MemoryServiceRegistration.ReplaceSingular<IMemoryEventDispatcher, TDispatcher>(services);

        /// <summary>Replaces the singular profile runtime selector.</summary>
        /// <typeparam name="TSelector">The selector implementation.</typeparam>
        /// <returns>The same service collection.</returns>
        public IServiceCollection ReplaceMemoryProfileRuntimeSelector<TSelector>()
            where TSelector : class, IMemoryProfileRuntimeSelector => MemoryServiceRegistration.ReplaceSingular<IMemoryProfileRuntimeSelector, TSelector>(services);
    }
}
