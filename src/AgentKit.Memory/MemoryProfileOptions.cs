// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Mutable configuration for one named memory profile.</summary>
/// <remarks>
/// <para>
/// A profile names the exact keyed collaborators an operation runs under. The keys are frozen into an immutable
/// <see cref="MemoryProfileSnapshot"/> when the provider is built, so a later registration affects later compositions only.
/// Enabling an axis without naming every collaborator it needs fails composition validation.
/// </para>
/// <para>Embedding and reranking are enabled only by a complete selected triple: a selector key, an executor key, and a non-empty ordered alias list. Supplying only part of a triple is a build error. Instances are mutated only while the service collection is configured.</para>
/// </remarks>
public sealed class MemoryProfileOptions
{
    /// <summary>Gets or sets the published revision of this profile's selection.</summary>
    /// <value>A positive revision recorded with every operation. Defaults to <c>1</c>.</value>
    public MemoryProfileVersion Version { get; set; } = new(1);

    /// <summary>Gets or sets whether durable memory is enabled; it requires <see cref="MemoryStore"/>.</summary>
    public bool EnableDurableMemory { get; set; }

    /// <summary>Gets or sets whether retrieval is enabled; it requires at least one entry in <see cref="RetrievalSources"/>.</summary>
    public bool EnableRetrieval { get; set; }

    /// <summary>Gets or sets whether query rewriting is enabled for this profile.</summary>
    /// <value>When enabled without <see cref="QueryRewriter"/>, the package's no-rewrite strategy is selected.</value>
    public bool EnableQueryRewriting { get; set; }

    /// <summary>Gets or sets whether each result must be authorized for model exposure, or <see langword="null"/> to inherit the engine setting.</summary>
    public bool? RequireExposureAuthorization { get; set; }

    /// <summary>Gets or sets the durable-memory store key.</summary>
    public MemoryStoreKey? MemoryStore { get; set; }

    /// <summary>Gets or sets the document store key.</summary>
    public DocumentStoreKey? DocumentStore { get; set; }

    /// <summary>Gets or sets the vector index keys.</summary>
    public List<VectorIndexKey> VectorIndexes { get; set; } = [];

    /// <summary>Gets or sets the retrieval source keys in selection order.</summary>
    public List<RetrievalSourceKey> RetrievalSources { get; set; } = [];

    /// <summary>Gets or sets the query rewriter key.</summary>
    public QueryRewriterKey? QueryRewriter { get; set; }

    /// <summary>Gets or sets the memory policy profile key.</summary>
    /// <value>Defaults to <see cref="MemoryPolicyProfileKeys.FailClosed"/>.</value>
    public MemoryPolicyProfileKey PolicyProfile { get; set; } = MemoryPolicyProfileKeys.FailClosed;

    /// <summary>Gets or sets the embedding selector key, one third of the embedding triple.</summary>
    public ComponentKey<IEmbeddingModelSelector>? EmbeddingSelectorKey { get; set; }

    /// <summary>Gets or sets the embedding executor key, one third of the embedding triple.</summary>
    public ComponentKey<IEmbeddingRequestExecutor>? EmbeddingExecutorKey { get; set; }

    /// <summary>Gets or sets the ordered embedding model aliases, one third of the embedding triple.</summary>
    public List<EmbeddingModelAlias> EmbeddingModels { get; set; } = [];

    /// <summary>Gets or sets the reranker selector key, one third of the reranker triple.</summary>
    public ComponentKey<IRerankerSelector>? RerankerSelectorKey { get; set; }

    /// <summary>Gets or sets the reranker executor key, one third of the reranker triple.</summary>
    public ComponentKey<IRerankRequestExecutor>? RerankerExecutorKey { get; set; }

    /// <summary>Gets or sets the ordered reranker aliases, one third of the reranker triple.</summary>
    public List<RerankerAlias> Rerankers { get; set; } = [];

    /// <summary>Gets or sets the retrieved-item bound, or <see langword="null"/> to inherit the engine ceiling; it may only narrow the ceiling.</summary>
    public int? MaximumRetrievedItems { get; set; }

    /// <summary>Gets or sets the retrieved-byte bound, or <see langword="null"/> to inherit the engine ceiling; it may only narrow the ceiling.</summary>
    public int? MaximumRetrievedBytes { get; set; }

    /// <summary>Gets or sets the retrieved-token bound, or <see langword="null"/> to inherit the engine ceiling; it may only narrow the ceiling.</summary>
    public int? MaximumRetrievalTokens { get; set; }

    /// <summary>Gets or sets the highest classification the profile may retain or expose.</summary>
    /// <value>Required: a profile that enables an axis without a classification ceiling fails composition validation.</value>
    public DataClassification? MaximumClassification { get; set; }
}
