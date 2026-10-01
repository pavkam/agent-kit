// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Tests;

/// <summary>Verifies exact profile activation, fail-closed capability checks, and lease ownership.</summary>
public sealed class DefaultMemoryProfileRuntimeSelectorTests
{
    private sealed class UnusedEmbeddingSelector: IEmbeddingModelSelector
    {
        public ValueTask<EmbeddingSelectionResult> SelectAsync(EmbeddingSelectionRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class UnusedEmbeddingExecutor: IEmbeddingRequestExecutor
    {
        public Task<EmbeddingExecutionResult> ExecuteAsync(EmbeddingExecutionRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private static IMemoryProfileRuntimeSelector Selector(MemoryHarness harness) => harness.Provider.GetRequiredService<IMemoryProfileRuntimeSelector>();

    private static MemoryOperationContext Context(MemoryTestOwner owner, string profile = "memory", long version = 1) => new(
        owner.AgentId, owner.SessionId, owner.Identity, owner.Context.Correlation, owner.Authorization, new MemoryProfileKey(profile), new MemoryProfileVersion(version));

    [Fact]
    public async Task SelectAsync_WhenProfileIsRegistered_ReturnsALeaseOverTheCapturedCollaborators()
    {
        using var harness = MemoryHarness.Create();
        var owner = MemoryTestData.NewOwner();

        var selection = await Selector(harness).SelectAsync(owner.Context, TestContext.Current.CancellationToken);

        var selected = selection.ShouldBeOfType<MemoryProfileRuntimeSelected>();
        await using var lease = selected.Runtime;
        lease.Profile.Key.ShouldBe(MemoryTestData.ProfileKey);
        lease.MemoryStore.ShouldBeSameAs(harness.Store);
        lease.DocumentStore.ShouldBeNull();
        lease.VectorIndexes.ShouldBeEmpty();
        lease.QueryRewriter.ShouldBeNull();
        lease.EmbeddingSelector.ShouldBeNull();
    }

    [Fact]
    public async Task SelectAsync_WhenProfileIsUnknown_ReportsUnknownProfile()
    {
        using var harness = MemoryHarness.Create();

        var selection = await Selector(harness).SelectAsync(Context(MemoryTestData.NewOwner(), "missing"), TestContext.Current.CancellationToken);

        selection.ShouldBeOfType<MemoryProfileRuntimeUnavailable>().Failure.Kind.ShouldBe(MemoryProfileRuntimeFailureKind.UnknownProfile);
    }

    [Fact]
    public async Task SelectAsync_WhenVersionDiffers_ReportsVersionMismatch()
    {
        using var harness = MemoryHarness.Create();

        var selection = await Selector(harness).SelectAsync(Context(MemoryTestData.NewOwner(), version: 9), TestContext.Current.CancellationToken);

        selection.ShouldBeOfType<MemoryProfileRuntimeUnavailable>().Failure.Kind.ShouldBe(MemoryProfileRuntimeFailureKind.VersionMismatch);
    }

    [Fact]
    public async Task SelectAsync_WhenTheNamedStoreIsNotRegistered_ReportsMissingCapability()
    {
        using var harness = MemoryHarness.Create(profile: configured => configured.MemoryStore = new MemoryStoreKey("absent"));

        var selection = await Selector(harness).SelectAsync(MemoryTestData.NewOwner().Context, TestContext.Current.CancellationToken);

        selection.ShouldBeOfType<MemoryProfileRuntimeUnavailable>().Failure.Kind.ShouldBe(MemoryProfileRuntimeFailureKind.MissingCapability);
    }

    [Fact]
    public async Task SelectAsync_WhenAStoreIsRegisteredUnderADifferentKeyThanItDeclares_ReportsInvalidComposition()
    {
        using var harness = MemoryHarness.Create(
            arrange: services => services.AddKeyedSingleton("alias", static (provider, _) => provider.GetRequiredKeyedService<IMemoryStore>(MemoryHarness.StoreKey.Value)),
            profile: configured => configured.MemoryStore = new MemoryStoreKey("alias"));

        var selection = await Selector(harness).SelectAsync(MemoryTestData.NewOwner().Context, TestContext.Current.CancellationToken);

        selection.ShouldBeOfType<MemoryProfileRuntimeUnavailable>().Failure.Kind.ShouldBe(MemoryProfileRuntimeFailureKind.InvalidComposition);
    }

    [Fact]
    public async Task SelectAsync_WhenTheNamedSourceIsNotRegistered_ReportsMissingCapability()
    {
        using var harness = MemoryHarness.Create(profile: configured => configured.RetrievalSources = [new RetrievalSourceKey("absent")]);

        var selection = await Selector(harness).SelectAsync(MemoryTestData.NewOwner().Context, TestContext.Current.CancellationToken);

        selection.ShouldBeOfType<MemoryProfileRuntimeUnavailable>().Failure.Kind.ShouldBe(MemoryProfileRuntimeFailureKind.MissingCapability);
    }

    [Fact]
    public async Task SelectAsync_WhenNoBudgetAuthorityIsRegistered_ReportsMissingCapability()
    {
        using var harness = MemoryHarness.Create(arrange: services => services.RemoveAll<IBudgetAuthority>());

        var selection = await Selector(harness).SelectAsync(MemoryTestData.NewOwner().Context, TestContext.Current.CancellationToken);

        selection.ShouldBeOfType<MemoryProfileRuntimeUnavailable>().Failure.Kind.ShouldBe(MemoryProfileRuntimeFailureKind.MissingCapability);
    }

    [Fact]
    public async Task SelectAsync_WhenEmbeddingIsNamedButNoExecutorIsRegistered_ReportsMissingCapability()
    {
        using var harness = MemoryHarness.Create(
            arrange: services => services.AddMemoryEmbeddingSelector<UnusedEmbeddingSelector>(new ComponentKey<IEmbeddingModelSelector>("embed-selector")),
            profile: configured =>
            {
                configured.EmbeddingSelectorKey = new ComponentKey<IEmbeddingModelSelector>("embed-selector");
                configured.EmbeddingExecutorKey = new ComponentKey<IEmbeddingRequestExecutor>("embed-executor");
                configured.EmbeddingModels = [new EmbeddingModelAlias("embed")];
            });

        var selection = await Selector(harness).SelectAsync(MemoryTestData.NewOwner().Context, TestContext.Current.CancellationToken);

        selection.ShouldBeOfType<MemoryProfileRuntimeUnavailable>().Failure.Kind.ShouldBe(MemoryProfileRuntimeFailureKind.MissingCapability);
    }

    [Fact]
    public async Task SelectAsync_WhenEmbeddingIsNamedButNoModelCatalogIsRegistered_ReportsMissingCapability()
    {
        using var harness = MemoryHarness.Create(
            arrange: services =>
            {
                _ = services.AddMemoryEmbeddingSelector<UnusedEmbeddingSelector>(new ComponentKey<IEmbeddingModelSelector>("embed-selector"));
                _ = services.AddMemoryEmbeddingExecutor<UnusedEmbeddingExecutor>(new ComponentKey<IEmbeddingRequestExecutor>("embed-executor"));
            },
            profile: configured =>
            {
                configured.EmbeddingSelectorKey = new ComponentKey<IEmbeddingModelSelector>("embed-selector");
                configured.EmbeddingExecutorKey = new ComponentKey<IEmbeddingRequestExecutor>("embed-executor");
                configured.EmbeddingModels = [new EmbeddingModelAlias("embed")];
            });

        var selection = await Selector(harness).SelectAsync(MemoryTestData.NewOwner().Context, TestContext.Current.CancellationToken);

        selection.ShouldBeOfType<MemoryProfileRuntimeUnavailable>().Failure.SafeMessage.ShouldContain("model catalog");
    }

    [Fact]
    public async Task SelectAsync_WhenContextIsNull_ThrowsArgumentNullException()
    {
        using var harness = MemoryHarness.Create();

        var exception = await Should.ThrowAsync<ArgumentNullException>(async () => await Selector(harness).SelectAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public async Task SelectAsync_WhenAlreadyCancelled_PropagatesCancellation()
    {
        using var harness = MemoryHarness.Create();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await Selector(harness).SelectAsync(MemoryTestData.NewOwner().Context, source.Token));
    }

    [Fact]
    public void Constructor_WhenArgumentsAreNull_ThrowArgumentNullException()
    {
        using var harness = MemoryHarness.Create();
        var catalog = harness.Provider.GetRequiredService<IMemoryProfileCatalog>();
        var options = Options.Create(new AgentMemoryOptions());

        Should.Throw<ArgumentNullException>(() => new DefaultMemoryProfileRuntimeSelector(null!, catalog, TimeProvider.System, options)).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => new DefaultMemoryProfileRuntimeSelector(harness.Provider, null!, TimeProvider.System, options)).ParamName.ShouldBe("profiles");
        Should.Throw<ArgumentNullException>(() => new DefaultMemoryProfileRuntimeSelector(harness.Provider, catalog, null!, options)).ParamName.ShouldBe("time");
        Should.Throw<ArgumentNullException>(() => new DefaultMemoryProfileRuntimeSelector(harness.Provider, catalog, TimeProvider.System, null!)).ParamName.ShouldBe("options");
    }
}
