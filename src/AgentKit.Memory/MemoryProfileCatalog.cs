// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

/// <summary>Compiles every registered profile into an immutable, validated <see cref="MemoryProfileSnapshot"/> once and serves them by key and version.</summary>
/// <remarks>
/// Compilation applies the engine ceilings: a profile may narrow the retrieval bounds but never widen them, and exposure authorization
/// cannot be turned off under an engine that requires it. Embedding and reranking are enabled only by a complete selector, executor, and
/// alias triple; a partial triple fails compilation, and compilation fails when an enabled axis omits a required key or the
/// classification ceiling. Compilation throws <see cref="InvalidOperationException"/>, so a misconfigured profile fails the build rather
/// than an operation.
/// </remarks>
internal sealed class MemoryProfileCatalog: IMemoryProfileCatalog
{
    private readonly Dictionary<string, MemoryProfileSnapshot> _snapshots = new(StringComparer.Ordinal);

    /// <summary>Initializes the catalog by compiling every registered profile.</summary>
    /// <param name="registry">The accumulated profile registrations.</param>
    /// <param name="rewriters">The registered query rewriter declarations whose versions profiles capture.</param>
    /// <param name="options">The engine-wide ceilings.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="InvalidOperationException">A profile is misconfigured.</exception>
    public MemoryProfileCatalog(MemoryProfileRegistry registry, IEnumerable<QueryRewriterDeclaration> rewriters, IOptions<AgentMemoryOptions> options)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(rewriters);
        ArgumentNullException.ThrowIfNull(options);
        var rewriterVersions = rewriters
            .GroupBy(static declaration => declaration.Descriptor.Key.Value, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Last().Descriptor, StringComparer.Ordinal);
        foreach (var (key, profile) in registry.Profiles)
        {
            _snapshots[key.Value] = Compile(key, profile, options.Value, rewriterVersions);
        }
    }

    /// <inheritdoc/>
    public bool TryGet(MemoryProfileKey key, [NotNullWhen(true)] out MemoryProfileSnapshot? profile)
    {
        profile = null;
        return key.Value is { Length: > 0 } value && _snapshots.TryGetValue(value, out profile);
    }

    /// <inheritdoc/>
    public bool TryGet(MemoryProfileKey key, MemoryProfileVersion version, [NotNullWhen(true)] out MemoryProfileSnapshot? profile)
    {
        if (TryGet(key, out var found) && found.Version == version)
        {
            profile = found;
            return true;
        }

        profile = null;
        return false;
    }

    private static MemoryProfileSnapshot Compile(
        MemoryProfileKey key,
        MemoryProfileOptions profile,
        AgentMemoryOptions engine,
        Dictionary<string, QueryRewriterDescriptor> rewriters)
    {
        var embedding = Triple(
            key, "embedding", profile.EmbeddingSelectorKey, profile.EmbeddingExecutorKey, profile.EmbeddingModels.Count,
            () => new EmbeddingRuntimeReference(profile.EmbeddingSelectorKey!.Value, profile.EmbeddingExecutorKey!.Value, new EmbeddingSelectionPolicy([.. profile.EmbeddingModels])));
        var reranker = Triple(
            key, "reranker", profile.RerankerSelectorKey, profile.RerankerExecutorKey, profile.Rerankers.Count,
            () => new RerankerRuntimeReference(profile.RerankerSelectorKey!.Value, profile.RerankerExecutorKey!.Value, new RerankerSelectionPolicy([.. profile.Rerankers])));
        if (profile.EnableDurableMemory && profile.MemoryStore is null)
        {
            throw Invalid(key, "Durable memory is enabled but no memory store key is named.");
        }

        if (profile.EnableRetrieval && profile.RetrievalSources.Count == 0)
        {
            throw Invalid(key, "Retrieval is enabled but no retrieval source key is named.");
        }

        var anyAxis = profile.EnableDurableMemory || profile.EnableRetrieval;
        if (anyAxis && profile.MaximumClassification is null)
        {
            throw Invalid(key, "An enabled axis requires a maximum classification ceiling.");
        }

        var rewriting = profile.EnableQueryRewriting || (profile.EnableRetrieval && engine.EnableQueryRewriting);
        QueryRewriterReference? rewriter = null;
        if (rewriting)
        {
            var rewriterKey = profile.QueryRewriter ?? NoRewriteQueryRewriter.Key;
            if (!rewriters.TryGetValue(rewriterKey.Value, out var descriptor))
            {
                throw Invalid(key, $"Query rewriting names rewriter '{rewriterKey.Value}', which is not registered.");
            }

            rewriter = new QueryRewriterReference(descriptor.Key, descriptor.Version);
        }

        var exposure = profile.RequireExposureAuthorization ?? engine.RequireExposureAuthorization;
        if (engine.RequireExposureAuthorization && !exposure)
        {
            throw Invalid(key, "A profile cannot disable exposure authorization while the engine requires it.");
        }

        var budget = new RetrievalBudget(
            Narrow(key, "MaximumRetrievedItems", profile.MaximumRetrievedItems, engine.MaximumRetrievedItems),
            Narrow(key, "MaximumRetrievedBytes", profile.MaximumRetrievedBytes, engine.MaximumRetrievedBytes),
            Narrow(key, "MaximumRetrievalTokens", profile.MaximumRetrievalTokens, engine.MaximumRetrievalTokens));
        var classification = profile.MaximumClassification ?? DataClassification.Public;
        var snapshot = new MemoryProfileSnapshot(
            key,
            profile.Version,
            profile.EnableDurableMemory,
            profile.EnableRetrieval,
            rewriting,
            exposure,
            profile.MemoryStore,
            profile.DocumentStore,
            [.. profile.VectorIndexes],
            [.. profile.RetrievalSources],
            rewriter,
            profile.PolicyProfile,
            embedding,
            reranker,
            budget,
            classification,
            new ContentHash("sha256:pending"));
        return new MemoryProfileSnapshot(
            snapshot.Key, snapshot.Version, snapshot.DurableMemoryEnabled, snapshot.RetrievalEnabled, snapshot.QueryRewritingEnabled,
            snapshot.RequireExposureAuthorization, snapshot.MemoryStore, snapshot.DocumentStore, snapshot.VectorIndexes, snapshot.RetrievalSources,
            snapshot.QueryRewriter, snapshot.PolicyProfile, snapshot.Embedding, snapshot.Reranker, snapshot.RetrievalBudget,
            snapshot.MaximumClassification, Fingerprint(snapshot));
    }

    private static TReference? Triple<TReference>(
        MemoryProfileKey key,
        string name,
        object? selector,
        object? executor,
        int aliases,
        Func<TReference> create)
        where TReference : class
    {
        var present = (selector is not null ? 1 : 0) + (executor is not null ? 1 : 0) + (aliases > 0 ? 1 : 0);
        return present switch
        {
            0 => null,
            3 => create(),
            _ => throw Invalid(key, $"The {name} selection must name a selector key, an executor key, and at least one alias together; a partial triple is not allowed."),
        };
    }

    private static int Narrow(MemoryProfileKey key, string name, int? requested, int ceiling)
    {
        return ceiling <= 0
            ? throw Invalid(key, $"The engine ceiling {name} must be positive.")
            : requested is null
                ? ceiling
                : requested.Value <= 0 || requested.Value > ceiling
                    ? throw Invalid(key, $"{name} must be positive and may only narrow the engine ceiling of {ceiling.ToString(CultureInfo.InvariantCulture)}.")
                    : requested.Value;
    }

    private static InvalidOperationException Invalid(MemoryProfileKey key, string message) =>
        new($"Memory profile '{key.Value}' is invalid: {message}");

    private static ContentHash Fingerprint(MemoryProfileSnapshot snapshot)
    {
        var text = new StringBuilder();
        _ = text.Append(snapshot.Key.Value).Append('|').Append(snapshot.Version.Value.ToString(CultureInfo.InvariantCulture))
            .Append('|').Append(snapshot.DurableMemoryEnabled).Append(snapshot.RetrievalEnabled).Append(snapshot.QueryRewritingEnabled).Append(snapshot.RequireExposureAuthorization)
            .Append('|').Append(snapshot.MemoryStore?.Value).Append('|').Append(snapshot.DocumentStore?.Value)
            .Append('|').AppendJoin(',', snapshot.VectorIndexes.Select(static index => index.Value))
            .Append('|').AppendJoin(',', snapshot.RetrievalSources.Select(static source => source.Value))
            .Append('|').Append(snapshot.QueryRewriter?.Key.Value).Append(':').Append(snapshot.QueryRewriter?.Version.Value)
            .Append('|').Append(snapshot.PolicyProfile.Value)
            .Append('|').Append(snapshot.Embedding?.SelectorKey.Value).Append(':').Append(snapshot.Embedding?.ExecutorKey.Value)
            .Append(':').AppendJoin(',', snapshot.Embedding?.Policy.Candidates.Select(static alias => alias.Value) ?? [])
            .Append('|').Append(snapshot.Reranker?.SelectorKey.Value).Append(':').Append(snapshot.Reranker?.ExecutorKey.Value)
            .Append(':').AppendJoin(',', snapshot.Reranker?.Policy.Candidates.Select(static alias => alias.Value) ?? [])
            .Append('|').Append(snapshot.RetrievalBudget.MaximumItems).Append(':').Append(snapshot.RetrievalBudget.MaximumBytes).Append(':').Append(snapshot.RetrievalBudget.MaximumTokens)
            .Append('|').Append((int) snapshot.MaximumClassification);
        return new ContentHash($"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))}");
    }
}
