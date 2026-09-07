// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

/// <summary>Verifies the content-free concrete session-store diagnostics contract.</summary>
public sealed class ObservabilityTests
{
    [Fact]
    public void ActivityObservation_WhenOperationNameIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ActivityObservation(
            null!,
            ActivityStatusCode.Ok,
            FrozenDictionary<string, object?>.Empty));

        exception.ParamName.ShouldBe("operationName");
    }

    [Fact]
    public void ActivityObservation_WhenOperationNameIsBlank_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ActivityObservation(
            " ",
            ActivityStatusCode.Ok,
            FrozenDictionary<string, object?>.Empty));

        exception.ParamName.ShouldBe("operationName");
    }

    [Fact]
    public void ActivityObservation_WhenStatusIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ActivityObservation(
            "test.operation",
            (ActivityStatusCode) int.MaxValue,
            FrozenDictionary<string, object?>.Empty));

        exception.ParamName.ShouldBe("status");
    }

    [Fact]
    public void ActivityObservation_WhenTagsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ActivityObservation(
            "test.operation",
            ActivityStatusCode.Ok,
            null!));

        exception.ParamName.ShouldBe("tags");
    }

    [Fact]
    public void GetTagItem_WhenKeyIsBlank_ThrowsArgumentException()
    {
        var observation = new ActivityObservation(
            "test.operation",
            ActivityStatusCode.Ok,
            FrozenDictionary<string, object?>.Empty);

        var exception = Should.Throw<ArgumentException>(() => observation.GetTagItem(" "));

        exception.ParamName.ShouldBe("key");
    }

    [Fact]
    public void ActivityCollector_WhenSourcePredicateIsNull_ThrowsBeforeListenerRegistration()
    {
        using var source = new ActivitySource("AgentKit.Test.Shared.Validation.Source");
        var exception = Should.Throw<ArgumentNullException>(() => new ActivityCollector(
            null!,
            static _ => true));

        exception.ParamName.ShouldBe("shouldListenTo");
        source.HasListeners().ShouldBeFalse();
    }

    [Fact]
    public void ActivityCollector_WhenOperationPredicateIsNull_ThrowsBeforeListenerRegistration()
    {
        using var source = new ActivitySource("AgentKit.Test.Shared.Validation.Operation");
        var exception = Should.Throw<ArgumentNullException>(() => new ActivityCollector(
            static _ => true,
            null!));

        exception.ParamName.ShouldBe("shouldCollect");
        source.HasListeners().ShouldBeFalse();
    }

    [Fact]
    public async Task CreateAsync_WhenObserved_EmitsCorrelatedStoreActivity()
    {
        var store = TestFactory.CreateStore();
        var request = TestFactory.CreateRequest();
        using var activities = CreateStoreOperationCollector(request.AgentId, "create");

        _ = await store.CreateAsync(request, TestContext.Current.CancellationToken);

        var activity = activities.Snapshot().ShouldHaveSingleItem();
        activity.OperationName.ShouldBe(AgentKitActivityNames.SessionStoreOperation);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.AgentId).ShouldBe(request.AgentId.ToString());
        activity.GetTagItem(AgentKitTagNames.SessionOperation).ShouldBe("create");
    }

    [Fact]
    public async Task ActivityCollector_WhenSourcesEmitConcurrently_IsolatesSourceAndOperationCorrelations()
    {
        using var firstSource = new ActivitySource("AgentKit.Test.Shared.First");
        using var secondSource = new ActivitySource("AgentKit.Test.Shared.Second");
        var firstCorrelation = Guid.NewGuid().ToString("N");
        var secondCorrelation = Guid.NewGuid().ToString("N");
        using var firstActivities = new ActivityCollector(
            source => ReferenceEquals(source, firstSource),
            activity => activity.GetTagItem("operation.correlation")?.Equals(firstCorrelation) == true);
        using var secondActivities = new ActivityCollector(
            source => ReferenceEquals(source, secondSource),
            activity => activity.GetTagItem("operation.correlation")?.Equals(secondCorrelation) == true);
        using var barrier = new Barrier(2);
        var cancellationToken = TestContext.Current.CancellationToken;

        await Task.WhenAll(
            Task.Run(
                () => EmitActivity(firstSource, "first", firstCorrelation, barrier, cancellationToken),
                cancellationToken),
            Task.Run(
                () => EmitActivity(secondSource, "second", secondCorrelation, barrier, cancellationToken),
                cancellationToken));

        var firstActivity = firstActivities.Snapshot().ShouldHaveSingleItem();
        var secondActivity = secondActivities.Snapshot().ShouldHaveSingleItem();
        firstActivity.OperationName.ShouldBe("first");
        secondActivity.OperationName.ShouldBe("second");
        firstActivity.GetTagItem("operation.correlation").ShouldBe(firstCorrelation);
        secondActivity.GetTagItem("operation.correlation").ShouldBe(secondCorrelation);
    }

    [Fact]
    public void ActivityCollector_WhenActivityContainsDuplicateTagKeys_RetainsTheFirstValue()
    {
        using var source = new ActivitySource("AgentKit.Test.Shared.DuplicateTags");
        using var activities = new ActivityCollector(
            candidate => ReferenceEquals(candidate, source),
            static _ => true);

        using (var activity = source.StartActivity("duplicate-tags"))
        {
            ArgumentNullException.ThrowIfNull(activity);
            _ = activity.AddTag("duplicate", "first");
            _ = activity.AddTag("duplicate", "second");
        }

        var observation = activities.Snapshot().ShouldHaveSingleItem();
        observation.GetTagItem("duplicate").ShouldBe("first");
    }

    private static ActivityCollector CreateStoreOperationCollector(AgentId agentId, string operation) => new(
        static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
        activity =>
            activity.OperationName == AgentKitActivityNames.SessionStoreOperation &&
            activity.GetTagItem(AgentKitTagNames.AgentId)?.Equals(agentId.ToString()) == true &&
            activity.GetTagItem(AgentKitTagNames.SessionOperation)?.Equals(operation) == true);

    private static void EmitActivity(
        ActivitySource source,
        string operationName,
        string correlation,
        Barrier barrier,
        CancellationToken cancellationToken)
    {
        using var activity = source.StartActivity(operationName);
        ArgumentNullException.ThrowIfNull(activity);
        _ = activity.SetTag("operation.correlation", correlation);
        barrier.SignalAndWait(cancellationToken);
    }
}
