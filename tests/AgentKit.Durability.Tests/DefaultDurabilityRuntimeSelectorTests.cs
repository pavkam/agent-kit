// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Tests;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies exact keyed activation and fail-closed behavior for partial durability compositions.</summary>
public sealed class DefaultDurabilityRuntimeSelectorTests
{
    [Fact]
    public async Task ActivateAsync_WhenTheContextIsNull_ThrowsArgumentNullException()
    {
        using var provider = Compose();
        var selector = new DefaultDurabilityRuntimeSelector(provider);

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await selector.ActivateAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public async Task ActivateAsync_WhenAlreadyCancelled_ThrowsOperationCanceledException()
    {
        using var provider = Compose();
        var selector = new DefaultDurabilityRuntimeSelector(provider);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await selector.ActivateAsync(DurableJournalTestData.Context(), cancellation.Token));
    }

    [Fact]
    public async Task ActivateAsync_WhenEveryCapturedKeyResolves_ActivatesThatExactComposition()
    {
        using var provider = Compose();
        var selector = new DefaultDurabilityRuntimeSelector(provider);

        var result = await selector.ActivateAsync(
            DurableJournalTestData.Context(),
            TestContext.Current.CancellationToken);

        var activated = result.ShouldBeOfType<DurabilityRuntimeActivated>();
        await using var runtime = activated.Lease;
        runtime.Backend.Descriptor.Key.ShouldBe(new DurableBackendKey("backend"));
        _ = runtime.Journal.ShouldBeOfType<RecordingDurableOperationJournal>();
        _ = runtime.LeaseManager.ShouldBeOfType<InMemoryDurableLeaseManager>();
        _ = runtime.RecoveryPolicy.ShouldBeOfType<DefaultRecoveryPolicy>();
        runtime.Context.ShouldBe(DurableJournalTestData.Context());
    }

    [Theory]
    [InlineData("backend")]
    [InlineData("journal")]
    [InlineData("leases")]
    [InlineData("policy")]
    public async Task ActivateAsync_WhenOneComponentIsMissing_FailsClosedInsteadOfActivatingAPartialRuntime(
        string omitted)
    {
        using var provider = Compose(omitted);
        var selector = new DefaultDurabilityRuntimeSelector(provider);

        var result = await selector.ActivateAsync(
            DurableJournalTestData.Context(),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DurabilityRuntimeActivationFailed>();
    }

    [Fact]
    public async Task ActivateAsync_WhenActivationFails_ReleasesTheScopedComponentsItAlreadyResolved()
    {
        // A refused activation must not leak the scope it opened while probing the captured keys.
        using var provider = Compose(omitted: "journal", scopedBackend: true);
        var tracker = provider.GetRequiredService<ScopeTracker>();
        var selector = new DefaultDurabilityRuntimeSelector(provider);

        _ = await selector.ActivateAsync(DurableJournalTestData.Context(), TestContext.Current.CancellationToken);

        tracker.Created.ShouldBe(1);
        tracker.Disposed.ShouldBe(1);
    }

    [Fact]
    public async Task DisposeAsync_WhenTheRuntimeLeaseIsReleased_ReleasesItsScopedComponentsExactlyOnce()
    {
        using var provider = Compose(scopedBackend: true);
        var tracker = provider.GetRequiredService<ScopeTracker>();
        var selector = new DefaultDurabilityRuntimeSelector(provider);
        var result = await selector.ActivateAsync(
            DurableJournalTestData.Context(),
            TestContext.Current.CancellationToken);
        var runtime = result.ShouldBeOfType<DurabilityRuntimeActivated>().Lease;

        await runtime.DisposeAsync();
        await runtime.DisposeAsync();

        tracker.Disposed.ShouldBe(1);
    }

    [Fact]
    public async Task ActivateAsync_WhenTheCapturedKeyNamesAnotherRegistration_NeverFallsBackToADifferentComponent()
    {
        // Resume must activate the composition the work started under, not the profile's current selection.
        using var provider = Compose();
        var selector = new DefaultDurabilityRuntimeSelector(provider);
        var context = DurableJournalTestData.Context() with { JournalKey = new DurableJournalKey("elsewhere") };

        var result = await selector.ActivateAsync(context, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DurabilityRuntimeActivationFailed>();
    }

    private static ServiceProvider Compose(string? omitted = null, bool scopedBackend = false)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton(new ScopeTracker());
        if (omitted != "backend" && scopedBackend)
        {
            _ = services.AddKeyedScoped<IDurableExecutionBackend, TrackedBackend>("backend");
        }
        else if (omitted != "backend")
        {
            _ = services.AddKeyedSingleton<IDurableExecutionBackend>(
                "backend",
                new InMemoryDurableExecutionBackend(new DurableBackendKey("backend")));
        }

        if (omitted != "journal")
        {
            _ = services.AddKeyedSingleton<IDurableOperationJournal>(
                "journal",
                new RecordingDurableOperationJournal());
        }

        if (omitted != "leases")
        {
            _ = services.AddKeyedSingleton<IDurableLeaseManager>(
                "leases",
                new InMemoryDurableLeaseManager(
                    new SequentialExecutionLeaseIdGenerator(),
                    new FakeTimeProvider(DurableJournalTestData.Now),
                    NullLogger<InMemoryDurableLeaseManager>.Instance));
        }

        if (omitted != "policy")
        {
            _ = services.AddKeyedSingleton<IRecoveryPolicy>(
                "policy",
                new DefaultRecoveryPolicy(Options.Create(new AgentDurabilityOptions())));
        }

        return services.BuildServiceProvider();
    }

    /// <summary>Counts activation scopes so a leaked scope is observable.</summary>
    private sealed class ScopeTracker
    {
        internal int Created;
        internal int Disposed;
    }

    /// <summary>A scoped backend that reports its own creation and disposal to the shared tracker.</summary>
    private sealed class TrackedBackend: IDurableExecutionBackend, IDisposable
    {
        private readonly ScopeTracker _tracker;
        private readonly InMemoryDurableExecutionBackend _inner =
            new(new DurableBackendKey("backend"));

        public TrackedBackend(ScopeTracker tracker)
        {
            ArgumentNullException.ThrowIfNull(tracker);
            _tracker = tracker;
            _ = Interlocked.Increment(ref tracker.Created);
        }

        public DurableBackendDescriptor Descriptor => _inner.Descriptor;

        public void Dispose() => Interlocked.Increment(ref _tracker.Disposed);

        public ValueTask<DurableDispatchResult> DispatchAsync(
            DurableDispatchRequest request,
            CancellationToken cancellationToken = default) =>
            _inner.DispatchAsync(request, cancellationToken);

        public ValueTask<DurableReconciliationResult> ReconcileAsync(
            DurableReconciliationRequest request,
            CancellationToken cancellationToken = default) =>
            _inner.ReconcileAsync(request, cancellationToken);
    }
}
