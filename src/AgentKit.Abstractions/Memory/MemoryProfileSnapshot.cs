// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Is the immutable, versioned selection of stores, sources, semantic operations, bounds, and policy that one memory profile names.</summary>
/// <remarks>
/// An agent selects one memory profile when its run plan is compiled. The snapshot is data: it names keyed collaborators but
/// holds none of them. The runtime selector turns it into an owned lease for one operation. Construction enforces that every
/// enabled axis names its required collaborators and that a rewriter is named exactly when rewriting is enabled.
/// </remarks>
public sealed record MemoryProfileSnapshot
{
    /// <summary>Initializes a validated snapshot.</summary>
    /// <param name="key">The profile key.</param>
    /// <param name="version">The positive profile version.</param>
    /// <param name="durableMemoryEnabled">Whether durable memory is enabled; it requires <paramref name="memoryStore"/>.</param>
    /// <param name="retrievalEnabled">Whether retrieval is enabled; it requires at least one retrieval source.</param>
    /// <param name="queryRewritingEnabled">Whether query rewriting is enabled; it requires <paramref name="queryRewriter"/>.</param>
    /// <param name="requireExposureAuthorization">Whether each result must be authorized for model exposure.</param>
    /// <param name="memoryStore">The durable-memory store key, or <see langword="null"/>.</param>
    /// <param name="documentStore">The document store key, or <see langword="null"/>.</param>
    /// <param name="vectorIndexes">The vector index keys, or default for none.</param>
    /// <param name="retrievalSources">The retrieval source keys in selection order, or default for none.</param>
    /// <param name="queryRewriter">The captured rewriter key and version, or <see langword="null"/> when rewriting is disabled.</param>
    /// <param name="policyProfile">The memory policy profile key.</param>
    /// <param name="embedding">The complete embedding triple, or <see langword="null"/>.</param>
    /// <param name="reranker">The complete reranker triple, or <see langword="null"/>.</param>
    /// <param name="retrievalBudget">The retrieval ceiling.</param>
    /// <param name="maximumClassification">The highest classification the profile may retain or expose.</param>
    /// <param name="configurationFingerprint">The fingerprint of the complete configuration.</param>
    /// <exception cref="ArgumentNullException">A required reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The version is not positive or the classification is undefined.</exception>
    /// <exception cref="ArgumentException">A key is blank or duplicated, an enabled axis lacks its collaborators, or the rewriter disagrees with the rewriting flag.</exception>
    public MemoryProfileSnapshot(
        MemoryProfileKey key,
        MemoryProfileVersion version,
        bool durableMemoryEnabled,
        bool retrievalEnabled,
        bool queryRewritingEnabled,
        bool requireExposureAuthorization,
        MemoryStoreKey? memoryStore,
        DocumentStoreKey? documentStore,
        ImmutableArray<VectorIndexKey> vectorIndexes,
        ImmutableArray<RetrievalSourceKey> retrievalSources,
        QueryRewriterReference? queryRewriter,
        MemoryPolicyProfileKey policyProfile,
        EmbeddingRuntimeReference? embedding,
        RerankerRuntimeReference? reranker,
        RetrievalBudget retrievalBudget,
        DataClassification maximumClassification,
        ContentHash configurationFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version.Value, nameof(version));
        if (memoryStore is { } store)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(store.Value, nameof(memoryStore));
        }

        if (documentStore is { } documents)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(documents.Value, nameof(documentStore));
        }

        var indexes = vectorIndexes.IsDefault ? [] : vectorIndexes;
        var sources = retrievalSources.IsDefault ? [] : retrievalSources;
        var seenIndexes = new HashSet<VectorIndexKey>();
        foreach (var index in indexes)
        {
            if (string.IsNullOrWhiteSpace(index.Value) || !seenIndexes.Add(index))
            {
                throw new ArgumentException("Vector index keys must be initialized and unique.", nameof(vectorIndexes));
            }
        }

        var seenSources = new HashSet<RetrievalSourceKey>();
        foreach (var source in sources)
        {
            if (string.IsNullOrWhiteSpace(source.Value) || !seenSources.Add(source))
            {
                throw new ArgumentException("Retrieval source keys must be initialized and unique.", nameof(retrievalSources));
            }
        }

        ArgumentException.ThrowIfNotEqual(!durableMemoryEnabled || memoryStore is not null, true, nameof(memoryStore));
        ArgumentException.ThrowIfNotEqual(!retrievalEnabled || !sources.IsEmpty, true, nameof(retrievalSources));
        ArgumentException.ThrowIfNotEqual(queryRewritingEnabled, queryRewriter is not null, nameof(queryRewriter));
        ArgumentException.ThrowIfNullOrWhiteSpace(policyProfile.Value, nameof(policyProfile));
        ArgumentNullException.ThrowIfNull(retrievalBudget);
        ArgumentOutOfRangeException.ThrowIfUndefined(maximumClassification);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationFingerprint.Value, nameof(configurationFingerprint));
        Key = key;
        Version = version;
        DurableMemoryEnabled = durableMemoryEnabled;
        RetrievalEnabled = retrievalEnabled;
        QueryRewritingEnabled = queryRewritingEnabled;
        RequireExposureAuthorization = requireExposureAuthorization;
        MemoryStore = memoryStore;
        DocumentStore = documentStore;
        VectorIndexes = indexes;
        RetrievalSources = sources;
        QueryRewriter = queryRewriter;
        PolicyProfile = policyProfile;
        Embedding = embedding;
        Reranker = reranker;
        RetrievalBudget = retrievalBudget;
        MaximumClassification = maximumClassification;
        ConfigurationFingerprint = configurationFingerprint;
    }

    /// <summary>Gets the profile key.</summary>
    public MemoryProfileKey Key { get; }

    /// <summary>Gets the profile version.</summary>
    public MemoryProfileVersion Version { get; }

    /// <summary>Gets a value indicating whether durable memory is enabled.</summary>
    public bool DurableMemoryEnabled { get; }

    /// <summary>Gets a value indicating whether retrieval is enabled.</summary>
    public bool RetrievalEnabled { get; }

    /// <summary>Gets a value indicating whether query rewriting is enabled.</summary>
    public bool QueryRewritingEnabled { get; }

    /// <summary>Gets a value indicating whether each result must be authorized for model exposure.</summary>
    public bool RequireExposureAuthorization { get; }

    /// <summary>Gets the durable-memory store key, or <see langword="null"/>.</summary>
    public MemoryStoreKey? MemoryStore { get; }

    /// <summary>Gets the document store key, or <see langword="null"/>.</summary>
    public DocumentStoreKey? DocumentStore { get; }

    /// <summary>Gets the vector index keys.</summary>
    public ImmutableArray<VectorIndexKey> VectorIndexes { get; }

    /// <summary>Gets the retrieval source keys in selection order.</summary>
    public ImmutableArray<RetrievalSourceKey> RetrievalSources { get; }

    /// <summary>Gets the captured rewriter key and version, or <see langword="null"/> when rewriting is disabled.</summary>
    public QueryRewriterReference? QueryRewriter { get; }

    /// <summary>Gets the memory policy profile key.</summary>
    public MemoryPolicyProfileKey PolicyProfile { get; }

    /// <summary>Gets the complete embedding triple, or <see langword="null"/>.</summary>
    public EmbeddingRuntimeReference? Embedding { get; }

    /// <summary>Gets the complete reranker triple, or <see langword="null"/>.</summary>
    public RerankerRuntimeReference? Reranker { get; }

    /// <summary>Gets the retrieval ceiling.</summary>
    public RetrievalBudget RetrievalBudget { get; }

    /// <summary>Gets the highest classification the profile may retain or expose.</summary>
    public DataClassification MaximumClassification { get; }

    /// <summary>Gets the fingerprint of the complete configuration.</summary>
    public ContentHash ConfigurationFingerprint { get; }

    /// <summary>Determines whether another snapshot is identical, comparing key lists by content.</summary>
    /// <param name="other">The snapshot to compare.</param>
    /// <returns><see langword="true"/> when every value matches.</returns>
    public bool Equals(MemoryProfileSnapshot? other) =>
        other is not null
        && Key == other.Key
        && Version == other.Version
        && DurableMemoryEnabled == other.DurableMemoryEnabled
        && RetrievalEnabled == other.RetrievalEnabled
        && QueryRewritingEnabled == other.QueryRewritingEnabled
        && RequireExposureAuthorization == other.RequireExposureAuthorization
        && MemoryStore == other.MemoryStore
        && DocumentStore == other.DocumentStore
        && VectorIndexes.SequenceEqual(other.VectorIndexes)
        && RetrievalSources.SequenceEqual(other.RetrievalSources)
        && Equals(QueryRewriter, other.QueryRewriter)
        && PolicyProfile == other.PolicyProfile
        && Equals(Embedding, other.Embedding)
        && Equals(Reranker, other.Reranker)
        && RetrievalBudget == other.RetrievalBudget
        && MaximumClassification == other.MaximumClassification
        && ConfigurationFingerprint == other.ConfigurationFingerprint;

    /// <summary>Returns a hash code consistent with <see cref="Equals(MemoryProfileSnapshot?)"/>.</summary>
    /// <returns>A hash over the identifying values and key lists.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Key);
        hash.Add(Version);
        hash.Add(ConfigurationFingerprint);
        foreach (var index in VectorIndexes)
        {
            hash.Add(index);
        }

        foreach (var source in RetrievalSources)
        {
            hash.Add(source);
        }

        return hash.ToHashCode();
    }
}
