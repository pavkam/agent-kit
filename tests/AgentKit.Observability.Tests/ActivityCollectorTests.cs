// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.Tests;

/// <summary>Verifies the shared activity collector used by observability tests across packages.</summary>
public sealed class ActivityCollectorTests
{
    [Fact]
    public void Constructor_WhenAPredicateIsNull_ThrowsArgumentNullExceptionNamingIt()
    {
        Should.Throw<ArgumentNullException>(() => new ActivityCollector(null!, static _ => true)).ParamName.ShouldBe("shouldListenTo");
        Should.Throw<ArgumentNullException>(() => new ActivityCollector(static _ => true, null!)).ParamName.ShouldBe("shouldCollect");
    }

    [Fact]
    public void Snapshot_WhenOnlyMatchingActivitiesStop_CapturesTheirTerminalStateAndTags()
    {
        var marker = Guid.NewGuid().ToString("N");
        using var collector = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => Equals(observation.GetTagItem("marker"), marker));

        using (var matching = AgentKitDiagnostics.Activities.StartActivity("collector.match"))
        {
            _ = matching.ShouldNotBeNull().SetTag("marker", marker);
            _ = matching.SetStatus(ActivityStatusCode.Error);
        }

        using (var other = AgentKitDiagnostics.Activities.StartActivity("collector.other"))
        {
            _ = other.ShouldNotBeNull().SetTag("marker", "different");
        }

        var observation = collector.Snapshot().ShouldHaveSingleItem();
        observation.OperationName.ShouldBe("collector.match");
        observation.Status.ShouldBe(ActivityStatusCode.Error);
        observation.GetTagItem("marker").ShouldBe(marker);
        observation.GetTagItem("absent").ShouldBeNull();
    }
}
