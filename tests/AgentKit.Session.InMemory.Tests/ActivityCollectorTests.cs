// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;



/// <summary>Verifies ActivityCollector behavior and contracts.</summary>
public sealed class ActivityCollectorTests
{
    [Fact]
    public void ActivityCollector_WhenSourcePredicateIsNull_ThrowsBeforeListenerRegistration()
    {
        using var source = new ActivitySource("AgentKit.Test.Shared.Validation.Source");
        var exception = Should.Throw<ArgumentNullException>(() => new ActivityCollector(null!, static _ => true));
        exception.ParamName.ShouldBe("shouldListenTo");
        source.HasListeners().ShouldBeFalse();
    }

    [Fact]
    public void ActivityCollector_WhenOperationPredicateIsNull_ThrowsBeforeListenerRegistration()
    {
        using var source = new ActivitySource("AgentKit.Test.Shared.Validation.Operation");
        var exception = Should.Throw<ArgumentNullException>(() => new ActivityCollector(static _ => true, null!));
        exception.ParamName.ShouldBe("shouldCollect");
        source.HasListeners().ShouldBeFalse();
    }

    [Fact]
    public async Task ActivityCollector_WhenSourcesEmitConcurrently_IsolatesSourceAndOperationCorrelations()
    {
        using var firstSource = new ActivitySource("AgentKit.Test.Shared.First");
        using var secondSource = new ActivitySource("AgentKit.Test.Shared.Second");
        var firstCorrelation = Guid.NewGuid().ToString("N");
        var secondCorrelation = Guid.NewGuid().ToString("N");
        using var firstActivities = new ActivityCollector(source => ReferenceEquals(source, firstSource), activity => activity.GetTagItem("operation.correlation")?.Equals(firstCorrelation) == true);
        using var secondActivities = new ActivityCollector(source => ReferenceEquals(source, secondSource), activity => activity.GetTagItem("operation.correlation")?.Equals(secondCorrelation) == true);
        using var barrier = new Barrier(2);
        var cancellationToken = TestContext.Current.CancellationToken;
        await Task.WhenAll(Task.Run(() => EmitActivity(firstSource, "first", firstCorrelation, barrier, cancellationToken), cancellationToken), Task.Run(() => EmitActivity(secondSource, "second", secondCorrelation, barrier, cancellationToken), cancellationToken));
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
        using var activities = new ActivityCollector(candidate => ReferenceEquals(candidate, source), static _ => true);
        using (var activity = source.StartActivity("duplicate-tags"))
        {
            ArgumentNullException.ThrowIfNull(activity);
            _ = activity.AddTag("duplicate", "first");
            _ = activity.AddTag("duplicate", "second");
        }

        var observation = activities.Snapshot().ShouldHaveSingleItem();
        observation.GetTagItem("duplicate").ShouldBe("first");
    }

    private static void EmitActivity(ActivitySource source, string operationName, string correlation, Barrier barrier, CancellationToken cancellationToken)
    {
        using var activity = source.StartActivity(operationName);
        ArgumentNullException.ThrowIfNull(activity);
        _ = activity.SetTag("operation.correlation", correlation);
        barrier.SignalAndWait(cancellationToken);
    }
}
