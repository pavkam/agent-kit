// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class DefaultSessionStoreRoutingTests
{
    [Fact]
    public void Constructor_WhenLoggerIsNull_ThrowsBeforeEnumeratingStores()
    {
        var store = new FakeSessionStore();
        var enumerationCount = 0;

        var exception = Should.Throw<ArgumentNullException>(() =>
            new DefaultSessionStoreSelector(Stores(), null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("logger");
        enumerationCount.ShouldBe(0);
        store.DescriptorReadCount.ShouldBe(0);
        return;

        IEnumerable<ISessionStore> Stores()
        {
            enumerationCount++;
            yield return store;
        }
    }

    [Fact]
    public void Constructor_WhenStoreKeysRepeat_ThrowsExactArgumentException()
    {
        var first = new FakeSessionStore();
        var second = new FakeSessionStore();

        var exception = Should.Throw<ArgumentException>(() => new DefaultSessionStoreSelector(
            [first, second],
            NullLogger<DefaultSessionStoreSelector>.Instance));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("stores");
    }

    [Fact]
    public void CatalogConstructor_WhenStoreDescriptorMutationIsInvalid_RejectsBeforeCapturingBinding()
    {
        var store = new FakeSessionStore();
        var descriptor = store.Descriptor;
        store.OnDescriptor = () => descriptor with { Key = default };

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultSessionStoreCatalog([store]));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("value");
        store.DescriptorReadCount.ShouldBe(2);
    }

    [Fact]
    public async Task SelectForCreateAsync_WhenProfileKeyIsComposed_ReturnsThatExactStore()
    {
        var store = new FakeSessionStore();
        var selector = new DefaultSessionStoreSelector([store], NullLogger<DefaultSessionStoreSelector>.Instance);

        var result = await selector.SelectForCreateAsync(new SessionStoreCreateSelectionRequest(
            TestFactory.CreateRequest(), Profile("fake")), TestContext.Current.CancellationToken);

        var selected = result.ShouldBeOfType<SessionStoreSelected>();
        selected.Store.ShouldBeSameAs(store);
    }

    [Fact]
    public async Task SelectForCreateAsync_WhenProfileKeyIsNotComposed_ReturnsTypedMissingKeyRejection()
    {
        var selector = new DefaultSessionStoreSelector(
            [new FakeSessionStore()],
            NullLogger<DefaultSessionStoreSelector>.Instance);

        var result = await selector.SelectForCreateAsync(new SessionStoreCreateSelectionRequest(
            TestFactory.CreateRequest(), Profile("missing")), TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionStoreSelectionRejected(SessionStoreSelectionRejectionReason.MissingStoreKey,
            "The selected session store is unavailable."));
    }

    [Fact]
    public async Task SelectForCreateAsync_WhenDurabilityIsRequired_ReturnsTypedDurabilityRejection()
    {
        var selector = new DefaultSessionStoreSelector(
            [new FakeSessionStore()],
            NullLogger<DefaultSessionStoreSelector>.Instance);

        var result = await selector.SelectForCreateAsync(
            new SessionStoreCreateSelectionRequest(TestFactory.CreateRequest(), Profile("fake", requiresDurableStore: true)),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionStoreSelectionRejected(
            SessionStoreSelectionRejectionReason.IncompatibleDurability,
            "The selected session store cannot satisfy the profile's durability requirement."));
    }

    [Fact]
    public async Task SelectForCreateAsync_WhenLoggerThrows_PreservesSelection()
    {
        var store = new FakeSessionStore();
        var selector = new DefaultSessionStoreSelector([store], new ThrowingLogger());

        var result = await selector.SelectForCreateAsync(
            new SessionStoreCreateSelectionRequest(TestFactory.CreateRequest(), Profile("fake")),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionStoreSelected>().Store.ShouldBeSameAs(store);
    }

    [Fact]
    public async Task SelectForCreateAsync_WhenCancelled_ThrowsOriginalCancellation()
    {
        var selector = new DefaultSessionStoreSelector(
            [new FakeSessionStore()],
            NullLogger<DefaultSessionStoreSelector>.Instance);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var exception = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await selector.SelectForCreateAsync(
                new SessionStoreCreateSelectionRequest(TestFactory.CreateRequest(), Profile("fake")),
                cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task SelectForCreateAsync_WhenMetricObserverThrows_PreservesSelection()
    {
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName
                    && instrument.Name == AgentKitMetricNames.SessionOperationCount)
                {
                    current.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            if (instrument.Name == AgentKitMetricNames.SessionOperationCount
                && HasSelectTag(tags))
            {
                throw new InvalidOperationException("observer");
            }
        });
        listener.Start();
        var store = new FakeSessionStore();
        var selector = new DefaultSessionStoreSelector([store], NullLogger<DefaultSessionStoreSelector>.Instance);

        var result = await selector.SelectForCreateAsync(
            new SessionStoreCreateSelectionRequest(TestFactory.CreateRequest(), Profile("fake")),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionStoreSelected>().Store.ShouldBeSameAs(store);
    }

    [Fact]
    public async Task SelectForCreateAsync_WhenObserved_EmitsContentFreeActivityAndBoundedMetricDimensions()
    {
        Activity? stopped = null;
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAll,
            ActivityStopped = activity => stopped = activity,
        };
        ActivitySource.AddActivityListener(activityListener);
        List<KeyValuePair<string, object?>> tags = [];
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName
                    && instrument.Name == AgentKitMetricNames.SessionOperationCount)
                {
                    current.EnableMeasurementEvents(instrument);
                }
            },
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, _, measurementTags, _) =>
        {
            if (instrument.Name == AgentKitMetricNames.SessionOperationCount
                && HasSelectTag(measurementTags))
            {
                tags.AddRange(measurementTags.ToArray());
            }
        });
        meterListener.Start();
        var selector = new DefaultSessionStoreSelector(
            [new FakeSessionStore()],
            NullLogger<DefaultSessionStoreSelector>.Instance);

        _ = await selector.SelectForCreateAsync(
            new SessionStoreCreateSelectionRequest(TestFactory.CreateRequest(), Profile("fake")),
            TestContext.Current.CancellationToken);

        var activity = stopped.ShouldNotBeNull();
        activity.OperationName.ShouldBe(AgentKitActivityNames.SessionStoreOperation);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.SessionOperation).ShouldBe("select");
        tags.Select(static tag => tag.Key).Distinct().ShouldBe(
            [AgentKitTagNames.SessionOperation, AgentKitTagNames.Outcome],
            ignoreOrder: true);
        tags.ShouldAllBe(static tag => tag.Value is string);
    }

    [Fact]
    public void GetDescriptors_WhenStoresAreProvided_ReturnsAStableKeyOrderedSnapshot()
    {
        var store = new FakeSessionStore();
        var catalog = new DefaultSessionStoreCatalog([store]);

        catalog.GetDescriptors().ShouldBe([store.Descriptor]);
    }

    [Fact]
    public async Task SharedSnapshot_WhenCatalogAndSelectorResolve_ReadsEachDescriptorOnce()
    {
        var store = new FakeSessionStore();
        var snapshot = new SessionStoreBindingSnapshot([store]);
        var catalog = new DefaultSessionStoreCatalog(snapshot);
        var selector = new DefaultSessionStoreSelector(snapshot, NullLogger<DefaultSessionStoreSelector>.Instance);

        var result = await selector.SelectForCreateAsync(
            new SessionStoreCreateSelectionRequest(TestFactory.CreateRequest(), Profile("fake")),
            TestContext.Current.CancellationToken);

        catalog.GetDescriptors().Length.ShouldBe(1);
        _ = result.ShouldBeOfType<SessionStoreSelected>();
        store.DescriptorReadCount.ShouldBe(1);
    }

    private static SessionProfileSnapshot Profile(string storeKey, bool requiresDurableStore = false) => new(
        new SessionProfileReference(new SessionProfileKey("profile"), new SessionProfileVersion(1)),
        new ComponentKey<ISessionCoordinator>("coordinator"),
        new ComponentKey<ISessionRunCoordinator>("run-coordinator"),
        new SessionStoreKey(storeKey), SessionStoreCapabilities.None, requiresDurableStore,
        requiresDistributedFencing: false, new SessionRetentionProfileKey("retention"), SessionBusyBehavior.Reject,
        maximumAppendEntries: 8, maximumPageSize: 16, verifySnapshotHashes: true, deleteOnDispose: false,
        new ContentHash("sha256:profile"));

    private sealed class ThrowingLogger: ILogger<DefaultSessionStoreSelector>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => throw new InvalidOperationException("observer");
    }

    private static ActivitySamplingResult SampleAll(ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllDataAndRecorded;

    private static bool HasSelectTag(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == AgentKitTagNames.SessionOperation && tag.Value?.ToString() == "select")
            {
                return true;
            }
        }

        return false;
    }
}
