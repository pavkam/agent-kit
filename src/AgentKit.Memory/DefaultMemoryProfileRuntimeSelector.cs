// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Activates the exact keyed stores, sources, rewriter, semantic operations, authority, budget, and dispatcher one memory profile version names.</summary>
/// <remarks>
/// <para>
/// Activation resolves exactly the keys frozen in the profile snapshot, never the agent's current configuration, so resuming
/// work cannot silently adopt a different composition. A missing collaborator, a collaborator registered under a key that its own
/// descriptor contradicts, or a rewriter whose version differs from the captured one fails closed with a typed
/// unavailable result before any I/O. The returned lease owns a child container scope and one operation-owned budget capability,
/// and the caller owns its disposal.
/// </para>
/// <para>The selector is the engine-wide routing boundary. It resolves services only through typed keys; it never exposes the service provider.</para>
/// </remarks>
internal sealed class DefaultMemoryProfileRuntimeSelector: IMemoryProfileRuntimeSelector
{
    private static readonly BudgetProfileKey _budgetProfile = new("agentkit.memory");
    private static readonly BudgetProfileVersion _budgetProfileVersion = new(1);
    private static readonly BudgetUnit _count = new("count");

    private readonly IServiceProvider _services;
    private readonly IMemoryProfileCatalog _profiles;
    private readonly TimeProvider _time;
    private readonly IOptions<AgentMemoryOptions> _options;
    private readonly ILogger<DefaultMemoryProfileRuntimeSelector> _logger;

    /// <summary>Initializes the selector.</summary>
    /// <param name="services">The container holding the keyed collaborator registrations.</param>
    /// <param name="profiles">The compiled profile snapshots.</param>
    /// <param name="time">The clock used only for observational duration.</param>
    /// <param name="options">The engine-wide options carrying the semantic-request ceiling.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public DefaultMemoryProfileRuntimeSelector(
        IServiceProvider services,
        IMemoryProfileCatalog profiles,
        TimeProvider time,
        IOptions<AgentMemoryOptions> options,
        ILogger<DefaultMemoryProfileRuntimeSelector>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(options);
        _services = services;
        _profiles = profiles;
        _time = time;
        _options = options;
        _logger = logger ?? NullLogger<DefaultMemoryProfileRuntimeSelector>.Instance;
    }

    /// <inheritdoc/>
    public async ValueTask<MemoryProfileRuntimeSelectionResult> SelectAsync(MemoryOperationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        using var activity = AgentKitActivityScope.Start(
            AgentKitActivityNames.MemoryProfileActivate,
            ActivityKind.Internal,
            [
                new(AgentKitTagNames.MemoryProfileKey, context.ProfileKey.Value),
                new(AgentKitTagNames.MemoryProfileVersion, context.ProfileVersion.Value),
                new(AgentKitTagNames.AgentId, context.AgentId.ToString()),
            ]);
        try
        {
            if (!_profiles.TryGet(context.ProfileKey, out var profile))
            {
                return Unavailable(activity, context, MemoryProfileRuntimeFailureKind.UnknownProfile, "The memory profile is not registered.");
            }

            if (profile.Version != context.ProfileVersion)
            {
                return Unavailable(activity, context, MemoryProfileRuntimeFailureKind.VersionMismatch, "The memory profile is registered at a different version.");
            }

            var scope = _services.CreateScope();
            try
            {
                var (collaborators, failure) = await ResolveAsync(scope.ServiceProvider, profile, context, cancellationToken).ConfigureAwait(false);
                if (collaborators is null)
                {
                    scope.Dispose();
                    return Unavailable(activity, context, failure!.Kind, failure.SafeMessage);
                }

                MemoryObservation.Safe(() => activity.Activity.SetSuccessful("activated"));
                return new MemoryProfileRuntimeSelected(new MemoryProfileRuntimeLease(scope, profile, collaborators));
            }
            catch
            {
                scope.Dispose();
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            MemoryObservation.Safe(() => activity.Activity.SetFailed("cancelled", nameof(OperationCanceledException)));
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            MemoryObservation.Safe(() => activity.Activity.SetFailed("faulted", errorType));
            throw;
        }
    }

    private MemoryProfileRuntimeUnavailable Unavailable(
        AgentKitActivityScope activity,
        MemoryOperationContext context,
        MemoryProfileRuntimeFailureKind kind,
        string message)
    {
        var name = kind switch
        {
            MemoryProfileRuntimeFailureKind.UnknownProfile => "unknown_profile",
            MemoryProfileRuntimeFailureKind.VersionMismatch => "version_mismatch",
            MemoryProfileRuntimeFailureKind.MissingCapability => "missing_capability",
            MemoryProfileRuntimeFailureKind.InvalidComposition => "invalid_composition",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The runtime failure class is undefined."),
        };
        MemoryObservation.Safe(() => activity.Activity.SetFailed(name, name));
        MemoryObservation.Safe(() => MemoryLog.ProfileUnavailable(_logger, context.ProfileKey.Value, context.ProfileVersion.Value, name));
        return new MemoryProfileRuntimeUnavailable(context.ProfileKey, context.ProfileVersion, new MemoryProfileRuntimeFailure(kind, message));
    }

    private async ValueTask<(MemoryRuntimeCollaborators? Collaborators, MemoryProfileRuntimeFailure? Failure)> ResolveAsync(
        IServiceProvider provider,
        MemoryProfileSnapshot profile,
        MemoryOperationContext context,
        CancellationToken cancellationToken)
    {
        IMemoryStore? memoryStore = null;
        if (profile.MemoryStore is { } memoryKey)
        {
            memoryStore = provider.GetKeyedService<IMemoryStore>(memoryKey.Value);
            if (memoryStore is null)
            {
                return Missing("The memory store the profile names is not registered.");
            }

            if (memoryStore.Descriptor.Key != memoryKey)
            {
                return Invalid("The registered memory store names a different key than it is registered under.");
            }
        }

        IDocumentStore? documentStore = null;
        if (profile.DocumentStore is { } documentKey)
        {
            documentStore = provider.GetKeyedService<IDocumentStore>(documentKey.Value);
            if (documentStore is null)
            {
                return Missing("The document store the profile names is not registered.");
            }

            if (documentStore.Descriptor.Key != documentKey)
            {
                return Invalid("The registered document store names a different key than it is registered under.");
            }
        }

        var indexes = ImmutableArray.CreateBuilder<IVectorIndex>();
        foreach (var indexKey in profile.VectorIndexes)
        {
            var index = provider.GetKeyedService<IVectorIndex>(indexKey.Value);
            if (index is null)
            {
                return Missing("A vector index the profile names is not registered.");
            }

            if (index.VectorSpace.IndexKey != indexKey)
            {
                return Invalid("A registered vector index names a different key than it is registered under.");
            }

            indexes.Add(index);
        }

        var sources = ImmutableArray.CreateBuilder<IRetrievalSource>();
        foreach (var sourceKey in profile.RetrievalSources)
        {
            var source = provider.GetKeyedService<IRetrievalSource>(sourceKey.Value);
            if (source is null)
            {
                return Missing("A retrieval source the profile names is not registered.");
            }

            if (source.Descriptor.Key != sourceKey)
            {
                return Invalid("A registered retrieval source names a different key than it is registered under.");
            }

            sources.Add(source);
        }

        IQueryRewriter? rewriter = null;
        if (profile.QueryRewriter is { } reference)
        {
            rewriter = provider.GetKeyedService<IQueryRewriter>(reference.Key.Value);
            if (rewriter is null)
            {
                return Missing("The query rewriter the profile names is not registered.");
            }

            if (!reference.Matches(rewriter.Descriptor))
            {
                return Invalid("The registered query rewriter is not the exact key and version the profile captured.");
            }
        }

        IEmbeddingModelSelector? embeddingSelector = null;
        IEmbeddingRequestExecutor? embeddingExecutor = null;
        if (profile.Embedding is { } embedding)
        {
            embeddingSelector = provider.GetKeyedService<IEmbeddingModelSelector>(embedding.SelectorKey.Value);
            embeddingExecutor = provider.GetKeyedService<IEmbeddingRequestExecutor>(embedding.ExecutorKey.Value);
            if (embeddingSelector is null || embeddingExecutor is null)
            {
                return Missing("The embedding selector or executor the profile names is not registered.");
            }
        }

        IRerankerSelector? rerankerSelector = null;
        IRerankRequestExecutor? rerankerExecutor = null;
        if (profile.Reranker is { } reranker)
        {
            rerankerSelector = provider.GetKeyedService<IRerankerSelector>(reranker.SelectorKey.Value);
            rerankerExecutor = provider.GetKeyedService<IRerankRequestExecutor>(reranker.ExecutorKey.Value);
            if (rerankerSelector is null || rerankerExecutor is null)
            {
                return Missing("The reranker selector or executor the profile names is not registered.");
            }
        }

        var authorities = provider.GetService<ISecurityAuthoritySelector>();
        var policy = provider.GetService<IRetrievalBudgetPolicy>();
        var events = provider.GetService<IMemoryEventDispatcher>();
        var budgets = provider.GetService<IBudgetAuthority>();
        if (authorities is null || policy is null || events is null || budgets is null)
        {
            return Missing("A required security authority, budget authority, budget policy, or event dispatcher is not registered.");
        }

        ModelCatalogSnapshot models;
        if (profile.Embedding is null && profile.Reranker is null)
        {
            models = new ModelCatalogSnapshot(new ModelCatalogVersion(1), []);
        }
        else
        {
            var catalog = provider.GetService<IModelCatalog>();
            if (catalog is null)
            {
                return Missing("The profile enables embedding or reranking but no model catalog is registered.");
            }

            models = await catalog.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        }

        var capability = await CreateBudgetAsync(budgets, context, cancellationToken).ConfigureAwait(false);
        return capability is null
            ? Missing("The budget authority could not create the operation's budget scope.")
            : (new MemoryRuntimeCollaborators(
                memoryStore, documentStore, indexes.ToImmutable(), sources.ToImmutable(), rewriter, embeddingSelector, embeddingExecutor,
                rerankerSelector, rerankerExecutor, models, authorities, capability, policy, events), null);
    }

    private async ValueTask<BudgetExecutionCapability?> CreateBudgetAsync(IBudgetAuthority budgets, MemoryOperationContext context, CancellationToken cancellationToken)
    {
        var correlation = context.Correlation;
        var address = new BudgetScopeAddress(
            context.Identity.TenantId,
            context.Identity.PrincipalId,
            context.AgentId,
            context.SessionId,
            (correlation as InRunOperationCorrelation)?.RunId,
            correlation.OperationId);
        var limits = ImmutableArray.Create(
            new BudgetLimit(BudgetDimensions.ModelRequests, _options.Value.MaximumSemanticRequestsPerOperation, _count, BudgetLimitKind.Hard));
        var idempotency = new IdempotencyKey($"agentkit.memory.budget:{context.ProfileKey.Value}:{_time.GetTimestamp()}:{Guid.NewGuid():N}");
        var created = await budgets.CreateChildScopeAsync(new BudgetScopeRequest(null, address, limits, idempotency), cancellationToken).ConfigureAwait(false);
        return created is BudgetScopeCreated scope
            ? new BudgetExecutionCapability(_budgetProfile, _budgetProfileVersion, context.Identity, correlation, scope.Scope)
            : null;
    }

    private static (MemoryRuntimeCollaborators?, MemoryProfileRuntimeFailure?) Missing(string message) =>
        (null, new MemoryProfileRuntimeFailure(MemoryProfileRuntimeFailureKind.MissingCapability, message));

    private static (MemoryRuntimeCollaborators?, MemoryProfileRuntimeFailure?) Invalid(string message) =>
        (null, new MemoryProfileRuntimeFailure(MemoryProfileRuntimeFailureKind.InvalidComposition, message));
}
