// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

/// <summary>Verifies that selecting a memory profile forces its whole runtime path to exist at composition.</summary>
public sealed class MemoryCompositionValidatorTests
{
    private static readonly MemoryProfileKey _profile = new("memory");

    private static readonly (Type Service, string Code)[] _singular =
    [
        (typeof(IMemoryCoordinator), "agentkit.memory.coordinator.missing"),
        (typeof(IRetrievalPipeline), "agentkit.memory.pipeline.missing"),
        (typeof(IMemoryProfileCatalog), "agentkit.memory.profile-catalog.missing"),
        (typeof(IMemoryProfileRuntimeSelector), "agentkit.memory.runtime-selector.missing"),
        (typeof(IMemoryPolicyDispatcher), "agentkit.memory.policy-dispatcher.missing"),
        (typeof(IMemoryEventDispatcher), "agentkit.memory.event-dispatcher.missing"),
        (typeof(IRetrievalBudgetPolicy), "agentkit.memory.budget-policy.missing"),
        (typeof(IMemoryStoreSelector), "agentkit.memory.store-selector.missing"),
        (typeof(ISecurityAuthoritySelector), "agentkit.memory.security-authority-selector.missing"),
        (typeof(IBudgetAuthority), "agentkit.memory.budget-authority.missing"),
        (typeof(TimeProvider), "agentkit.memory.time-provider.missing"),
    ];

    public static TheoryData<Type, string> SingularServices
    {
        get
        {
            var data = new TheoryData<Type, string>();
            foreach (var (service, code) in _singular)
            {
                data.Add(service, code);
            }

            return data;
        }
    }

    [Fact]
    public void Validate_WhenNoDefinitionSelectsAMemoryProfile_ReportsNothing() =>
        Validate(Definitions(memory: false), catalog: null, failure: null, Registrations(complete: false)).ShouldBeEmpty();

    [Fact]
    public void Validate_WhenTheCompositionIsComplete_ReportsNothing() =>
        Validate(Definitions(memory: true), new StubCatalog(_profile), failure: null, Registrations(complete: true)).ShouldBeEmpty();

    [Fact]
    public void Validate_WhenNoProfileCatalogIsRegistered_ReportsOnlyTheMissingProfile() =>
        Validate(Definitions(memory: true), catalog: null, failure: null, Registrations(complete: true))
            .Select(static diagnostic => diagnostic.Code).ShouldBe(["agentkit.memory.profile.missing"]);

    [Fact]
    public void Validate_WhenTheSelectedProfileIsNotPublished_ReportsTheMissingProfile() =>
        Validate(Definitions(memory: true), new StubCatalog(new MemoryProfileKey("other")), failure: null, Registrations(complete: true))
            .Select(static diagnostic => diagnostic.Code).ShouldBe(["agentkit.memory.profile.missing"]);

    [Fact]
    public void Validate_WhenProfileCompilationFailed_ReportsTheInvalidProfileOnce()
    {
        var diagnostics = Validate(Definitions(memory: true, count: 2), catalog: null, failure: "Memory profile 'memory' is invalid: boom.", Registrations(complete: true));

        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldBe(["agentkit.memory.profile.invalid"]);
        diagnostics[0].SafeMessage.ShouldContain("boom");
    }

    [Theory]
    [MemberData(nameof(SingularServices))]
    public void Validate_WhenASingularServiceIsMissing_ReportsThatExactDiagnostic(Type omitted, string code) =>
        Validate(Definitions(memory: true), new StubCatalog(_profile), failure: null, Registrations(complete: true, omitUnkeyed: omitted))
            .Select(static diagnostic => diagnostic.Code).ShouldBe([code]);

    [Theory]
    [InlineData("store", "agentkit.memory.store.missing")]
    [InlineData("documents", "agentkit.memory.document-store.missing")]
    [InlineData("index", "agentkit.memory.vector-index.missing")]
    [InlineData("source", "agentkit.memory.source.missing")]
    [InlineData("rewriter", "agentkit.memory.rewriter.missing")]
    [InlineData("embedding-selector", "agentkit.memory.embedding-selector.missing")]
    [InlineData("embedding-executor", "agentkit.memory.embedding-executor.missing")]
    [InlineData("reranker-selector", "agentkit.memory.reranker-selector.missing")]
    [InlineData("reranker-executor", "agentkit.memory.reranker-executor.missing")]
    public void Validate_WhenAKeyedComponentIsMissing_ReportsThatExactDiagnostic(string omitted, string code) =>
        Validate(Definitions(memory: true), new StubCatalog(_profile), failure: null, Registrations(complete: true, omitKeyed: omitted))
            .Select(static diagnostic => diagnostic.Code).ShouldBe([code]);

    [Fact]
    public void Validate_WhenAnEmbeddingOrRerankerIsNamedWithoutAModelCatalog_ReportsTheMissingCatalog() =>
        Validate(Definitions(memory: true), new StubCatalog(_profile), failure: null, Registrations(complete: true, omitUnkeyed: typeof(IModelCatalog)))
            .Select(static diagnostic => diagnostic.Code).ShouldBe(["agentkit.memory.model-catalog.missing"]);

    [Fact]
    public void Validate_WhenVectorIndexesAreNamedWithoutAnEmbeddingSelection_ReportsTheMissingEmbedding()
    {
        var catalog = new StubCatalog(_profile, withEmbedding: false);

        var diagnostics = Validate(Definitions(memory: true), catalog, failure: null, Registrations(complete: true));

        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldContain("agentkit.memory.embedding.missing");
    }

    [Fact]
    public void Validate_WhenAComponentIsRegisteredUnkeyed_StillRequiresTheProfilesExactKey()
    {
        var services = Services(complete: true, omitKeyed: "store");
        _ = services.AddSingleton<IMemoryStore>(static _ => throw new InvalidOperationException("never resolved"));

        var diagnostics = Validate(Definitions(memory: true), new StubCatalog(_profile), failure: null, ComponentRegistrationSnapshot.Capture(services));

        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldBe(["agentkit.memory.store.missing"]);
    }

    [Fact]
    public void Validate_WhenNothingIsRegistered_ReportsEveryEngineWideGapExactlyOnce()
    {
        var diagnostics = Validate(Definitions(memory: true, count: 2), new StubCatalog(_profile), failure: null, Registrations(complete: false));

        diagnostics.Count(static diagnostic => diagnostic.Code == "agentkit.memory.coordinator.missing").ShouldBe(1);
        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldContain("agentkit.memory.store.missing");
        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldContain("agentkit.memory.source.missing");
    }

    [Fact]
    public void Validate_WhenAProfileIsSelected_NeverActivatesAnyComponent()
    {
        var activations = 0;
        var services = Services(complete: true, omitKeyed: "store");
        _ = services.AddKeyedSingleton<IMemoryStore>("store", (_, _) =>
        {
            activations++;
            throw new InvalidOperationException("Composition validation must not activate the store.");
        });

        var diagnostics = Validate(Definitions(memory: true), new StubCatalog(_profile), failure: null, ComponentRegistrationSnapshot.Capture(services));

        diagnostics.ShouldBeEmpty();
        activations.ShouldBe(0);
    }

    private static ImmutableArray<CompositionDiagnostic> Validate(
        ImmutableArray<AgentDefinition> definitions, IMemoryProfileCatalog? catalog, string? failure, ComponentRegistrationSnapshot registrations)
    {
        var diagnostics = ImmutableArray.CreateBuilder<CompositionDiagnostic>();
        MemoryCompositionValidator.Validate(definitions, catalog, failure, registrations, diagnostics);
        return diagnostics.ToImmutable();
    }

    private static ImmutableArray<AgentDefinition> Definitions(bool memory, int count = 1) =>
        [.. Enumerable.Range(1, count).Select(index =>
        {
            var definition = CompositionTestData.Definition(new AgentId(Guid.Parse($"c1000000-0000-0000-0000-{index:D12}")));
            return memory ? definition with { OptionalCapabilities = new AgentOptionalCapabilitySelection(null, null, null, _profile, null, []) } : definition;
        })];

    private static ComponentRegistrationSnapshot Registrations(bool complete, Type? omitUnkeyed = null, string? omitKeyed = null) =>
        ComponentRegistrationSnapshot.Capture(Services(complete, omitUnkeyed, omitKeyed));

    private static ServiceCollection Services(bool complete, Type? omitUnkeyed = null, string? omitKeyed = null)
    {
        var services = new ServiceCollection();
        if (!complete)
        {
            return services;
        }

        foreach (var (service, _) in _singular.Append((typeof(IModelCatalog), string.Empty)))
        {
            if (service != omitUnkeyed)
            {
                _ = services.Add(ServiceDescriptor.Singleton(service, static _ => throw new InvalidOperationException("never resolved")));
            }
        }

        AddKeyed<IMemoryStore>(services, "store", omitKeyed);
        AddKeyed<IDocumentStore>(services, "documents", omitKeyed);
        AddKeyed<IVectorIndex>(services, "index", omitKeyed);
        AddKeyed<IRetrievalSource>(services, "source", omitKeyed);
        AddKeyed<IQueryRewriter>(services, "rewriter", omitKeyed);
        AddKeyed<IEmbeddingModelSelector>(services, "embedding-selector", omitKeyed);
        AddKeyed<IEmbeddingRequestExecutor>(services, "embedding-executor", omitKeyed);
        AddKeyed<IRerankerSelector>(services, "reranker-selector", omitKeyed);
        AddKeyed<IRerankRequestExecutor>(services, "reranker-executor", omitKeyed);
        return services;
    }

    private static void AddKeyed<TService>(ServiceCollection services, string name, string? omitted)
        where TService : class
    {
        if (name != omitted)
        {
            _ = services.AddKeyedSingleton<TService>(name, static (_, _) => throw new InvalidOperationException("never resolved"));
        }
    }

    /// <summary>Publishes exactly one profile that names one of every keyed collaborator.</summary>
    private sealed class StubCatalog(MemoryProfileKey registered, bool withEmbedding = true): IMemoryProfileCatalog
    {
        public bool TryGet(MemoryProfileKey key, [NotNullWhen(true)] out MemoryProfileSnapshot? profile)
        {
            profile = key == registered ? Snapshot() : null;
            return profile is not null;
        }

        public bool TryGet(MemoryProfileKey key, MemoryProfileVersion version, [NotNullWhen(true)] out MemoryProfileSnapshot? profile)
        {
            profile = key == registered && version == new MemoryProfileVersion(1) ? Snapshot() : null;
            return profile is not null;
        }

        private MemoryProfileSnapshot Snapshot() => MemoryTestData.Snapshot(
            registered.Value,
            memoryStore: "store",
            documentStore: "documents",
            vectorIndexes: ["index"],
            sources: ["source"],
            rewriter: new QueryRewriterReference(new QueryRewriterKey("rewriter"), new QueryRewriterVersion("1")),
            embedding: withEmbedding
                ? new EmbeddingRuntimeReference(
                    new ComponentKey<IEmbeddingModelSelector>("embedding-selector"), new ComponentKey<IEmbeddingRequestExecutor>("embedding-executor"),
                    new EmbeddingSelectionPolicy([new EmbeddingModelAlias("embed")]))
                : null,
            reranker: new RerankerRuntimeReference(
                new ComponentKey<IRerankerSelector>("reranker-selector"), new ComponentKey<IRerankRequestExecutor>("reranker-executor"),
                new RerankerSelectionPolicy([new RerankerAlias("rerank")])));
    }
}
