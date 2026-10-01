// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Validates that every agent definition selecting a memory profile resolves a complete, registered memory composition.</summary>
/// <remarks>
/// Validation is additive and content-free: it inspects build-local registration evidence and the compiled profile snapshot, never
/// resolves a store, index, source, or model, and performs no I/O. An agent without a memory profile has no durable-memory or
/// retrieval capability and is not inspected.
/// </remarks>
internal static class MemoryCompositionValidator
{
    /// <summary>Appends a diagnostic for every missing runtime service, profile, or keyed collaborator a selected memory profile needs.</summary>
    /// <param name="definitions">The materialized definition set.</param>
    /// <param name="profileCatalog">The compiled profile catalog, or null when none is registered.</param>
    /// <param name="catalogFailure">The safe message of a profile-compilation failure, or null when the catalog compiled.</param>
    /// <param name="componentRegistrations">The build-local registration evidence.</param>
    /// <param name="diagnostics">The caller-owned diagnostic builder.</param>
    internal static void Validate(
        ImmutableArray<AgentDefinition> definitions,
        IMemoryProfileCatalog? profileCatalog,
        string? catalogFailure,
        ComponentRegistrationSnapshot componentRegistrations,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        Debug.Assert(!definitions.IsDefault, "Memory validation runs only over a materialized definition set.");
        Debug.Assert(componentRegistrations is not null, "Memory validation requires registration evidence.");
        Debug.Assert(diagnostics is not null, "Memory validation appends to a caller-owned builder.");
        var validatedEngineWide = false;
        var validatedProfiles = new HashSet<MemoryProfileKey>();
        var reportedCatalogFailure = false;
        foreach (var definition in definitions)
        {
            if (definition.OptionalCapabilities.MemoryProfile is not { } profileKey)
            {
                continue;
            }

            if (!validatedEngineWide)
            {
                ValidateEngineWide(componentRegistrations, diagnostics);
                validatedEngineWide = true;
            }

            if (catalogFailure is not null)
            {
                if (!reportedCatalogFailure)
                {
                    diagnostics.Add(new CompositionDiagnostic("agentkit.memory.profile.invalid", catalogFailure));
                    reportedCatalogFailure = true;
                }

                continue;
            }

            if (profileCatalog is null || !profileCatalog.TryGet(profileKey, out var profile))
            {
                diagnostics.Add(new CompositionDiagnostic(
                    "agentkit.memory.profile.missing",
                    $"Agent '{definition.Id}' selects memory profile '{profileKey.Value}' but it is not registered."));
                continue;
            }

            if (!validatedProfiles.Add(profileKey))
            {
                continue;
            }

            ValidateProfile(profile, definition.Id, componentRegistrations, diagnostics);
        }
    }

    private static void ValidateProfile(
        MemoryProfileSnapshot profile,
        AgentId agentId,
        ComponentRegistrationSnapshot componentRegistrations,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        if (profile.MemoryStore is { } memoryStore)
        {
            RequireKeyed<IMemoryStore>(componentRegistrations, memoryStore.Value, agentId, profile.Key, "agentkit.memory.store.missing", diagnostics);
        }

        if (profile.DocumentStore is { } documentStore)
        {
            RequireKeyed<IDocumentStore>(componentRegistrations, documentStore.Value, agentId, profile.Key, "agentkit.memory.document-store.missing", diagnostics);
        }

        foreach (var index in profile.VectorIndexes)
        {
            RequireKeyed<IVectorIndex>(componentRegistrations, index.Value, agentId, profile.Key, "agentkit.memory.vector-index.missing", diagnostics);
        }

        foreach (var source in profile.RetrievalSources)
        {
            RequireKeyed<IRetrievalSource>(componentRegistrations, source.Value, agentId, profile.Key, "agentkit.memory.source.missing", diagnostics);
        }

        if (profile.QueryRewriter is { } rewriter)
        {
            RequireKeyed<IQueryRewriter>(componentRegistrations, rewriter.Key.Value, agentId, profile.Key, "agentkit.memory.rewriter.missing", diagnostics);
        }

        if (!profile.VectorIndexes.IsEmpty && profile.Embedding is null)
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.memory.embedding.missing",
                $"Agent '{agentId}' selects memory profile '{profile.Key.Value}', which names vector indexes but no embedding model selection."));
        }

        if (profile.Embedding is { } embedding)
        {
            RequireKeyed<IEmbeddingModelSelector>(componentRegistrations, embedding.SelectorKey.Value, agentId, profile.Key, "agentkit.memory.embedding-selector.missing", diagnostics);
            RequireKeyed<IEmbeddingRequestExecutor>(componentRegistrations, embedding.ExecutorKey.Value, agentId, profile.Key, "agentkit.memory.embedding-executor.missing", diagnostics);
        }

        if (profile.Reranker is { } reranker)
        {
            RequireKeyed<IRerankerSelector>(componentRegistrations, reranker.SelectorKey.Value, agentId, profile.Key, "agentkit.memory.reranker-selector.missing", diagnostics);
            RequireKeyed<IRerankRequestExecutor>(componentRegistrations, reranker.ExecutorKey.Value, agentId, profile.Key, "agentkit.memory.reranker-executor.missing", diagnostics);
        }

        if (profile.Embedding is not null || profile.Reranker is not null)
        {
            RequireUnkeyed<IModelCatalog>(componentRegistrations, "agentkit.memory.model-catalog.missing", diagnostics);
        }
    }

    private static void ValidateEngineWide(ComponentRegistrationSnapshot componentRegistrations, ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        Debug.Assert(componentRegistrations is not null, "Engine-wide validation requires registration evidence.");
        RequireUnkeyed<IMemoryCoordinator>(componentRegistrations, "agentkit.memory.coordinator.missing", diagnostics);
        RequireUnkeyed<IRetrievalPipeline>(componentRegistrations, "agentkit.memory.pipeline.missing", diagnostics);
        RequireUnkeyed<IMemoryProfileCatalog>(componentRegistrations, "agentkit.memory.profile-catalog.missing", diagnostics);
        RequireUnkeyed<IMemoryProfileRuntimeSelector>(componentRegistrations, "agentkit.memory.runtime-selector.missing", diagnostics);
        RequireUnkeyed<IMemoryPolicyDispatcher>(componentRegistrations, "agentkit.memory.policy-dispatcher.missing", diagnostics);
        RequireUnkeyed<IMemoryEventDispatcher>(componentRegistrations, "agentkit.memory.event-dispatcher.missing", diagnostics);
        RequireUnkeyed<IRetrievalBudgetPolicy>(componentRegistrations, "agentkit.memory.budget-policy.missing", diagnostics);
        RequireUnkeyed<IMemoryStoreSelector>(componentRegistrations, "agentkit.memory.store-selector.missing", diagnostics);
        RequireUnkeyed<ISecurityAuthoritySelector>(componentRegistrations, "agentkit.memory.security-authority-selector.missing", diagnostics);
        RequireUnkeyed<IBudgetAuthority>(componentRegistrations, "agentkit.memory.budget-authority.missing", diagnostics);
        RequireUnkeyed<TimeProvider>(componentRegistrations, "agentkit.memory.time-provider.missing", diagnostics);
    }

    private static void RequireUnkeyed<TService>(ComponentRegistrationSnapshot componentRegistrations, string code, ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
        where TService : class
    {
        var registered = componentRegistrations.Services.Any(static service => !service.IsKeyedService && service.ServiceType == typeof(TService));
        if (!registered)
        {
            diagnostics.Add(new CompositionDiagnostic(
                code,
                $"A memory profile is selected but no {typeof(TService).Name} is registered. Call AddAgentMemory and register the required engine services."));
        }
    }

    private static void RequireKeyed<TService>(
        ComponentRegistrationSnapshot componentRegistrations,
        string key,
        AgentId agentId,
        MemoryProfileKey profileKey,
        string code,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
        where TService : class
    {
        var registered = componentRegistrations.Services.Any(service =>
            service.IsKeyedService
            && service.ServiceType == typeof(TService)
            && key.Equals(service.ServiceKey as string, StringComparison.Ordinal));
        if (!registered)
        {
            diagnostics.Add(new CompositionDiagnostic(
                code,
                $"Agent '{agentId}' selects memory profile '{profileKey.Value}', which names {typeof(TService).Name} key '{key}', but no keyed registration exists for it."));
        }
    }
}
