// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using System.Diagnostics.Metrics;

using AgentKit.TestSupport;

using Microsoft.Extensions.Logging;

public sealed class ToolDiscoveryObservationTests
{
    [Theory]
    [InlineData(AgentKitActivityNames.ToolCatalogDiscover, "discovered")]
    [InlineData(AgentKitActivityNames.ToolCatalogDiscover, "cancelled")]
    [InlineData(AgentKitActivityNames.ToolCatalogDiscover, "failed")]
    [InlineData(AgentKitActivityNames.ToolCatalogDiscoverSource, "discovered")]
    [InlineData(AgentKitActivityNames.ToolCatalogDiscoverSource, "cancelled")]
    [InlineData(AgentKitActivityNames.ToolCatalogDiscoverSource, "failed")]
    [InlineData(AgentKitActivityNames.ToolCatalogDiscoveryTransfer, "transferred")]
    [InlineData(AgentKitActivityNames.ToolCatalogDiscoveryTransfer, "cancelled")]
    [InlineData(AgentKitActivityNames.ToolCatalogDiscoveryTransfer, "failed")]
    [InlineData(AgentKitActivityNames.ToolCatalogDiscoveryClose, "closed")]
    [InlineData(AgentKitActivityNames.ToolCatalogDiscoveryClose, "failed")]
    [InlineData(AgentKitActivityNames.ToolCatalogDiscoveryDisposeSources, "disposed")]
    [InlineData(AgentKitActivityNames.ToolCatalogDiscoveryDisposeSources, "failed")]
    [InlineData(AgentKitActivityNames.ToolCatalogDiscoveryDisposeSource, "disposed")]
    [InlineData(AgentKitActivityNames.ToolCatalogDiscoveryDisposeSource, "failed")]
    public void Complete_WhenObserved_RecordsOneSafeCorrelatedTerminalResult(string operation, string outcome)
    {
        var request = ToolCaptureTestData.Discovery();
        var logger = new RecordingLogger<ToolCatalogDiscovery>();
        var ticks = 0L;
        ToolSourceId? sourceId = operation is AgentKitActivityNames.ToolCatalogDiscoverSource or AgentKitActivityNames.ToolCatalogDiscoveryDisposeSource ? new ToolSourceId("observed-source") : null;
        using var parent = new Activity("discovery-observation-test").Start();
        Activity? observed = null;
        using var listener = Listen(parent, activity => observed = activity);
        List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> measured = [];
        using var metrics = ListenMetrics(parent, (name, value, tags) => measured.Add((name, value, tags)));
        var observation = new ToolDiscoveryObservation(operation, request, new CallbackTimestampTimeProvider(() => Interlocked.Add(ref ticks, 125)), logger, sourceId);
        observation.Complete(outcome);
        observation.Complete("failed");
        observation.Dispose();
        observation.Dispose();

        var activity = observed.ShouldNotBeNull();
        activity.OperationName.ShouldBe(operation);
        activity.ParentId.ShouldBe(parent.Id);
        activity.Status.ShouldBe(outcome is "cancelled" or "failed" ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe(outcome);
        activity.GetTagItem(AgentKitTagNames.TenantId).ShouldBe(request.Identity.TenantId.Value);
        activity.GetTagItem(AgentKitTagNames.PrincipalId).ShouldBe(request.Identity.PrincipalId.Value);
        activity.GetTagItem(AgentKitTagNames.AgentId).ShouldBe(request.AgentId.ToString());
        activity.GetTagItem(AgentKitTagNames.SessionId).ShouldBe(request.SessionId.ToString());
        activity.GetTagItem(AgentKitTagNames.RunId).ShouldBe(request.RunId.ToString());
        if (outcome is "cancelled" or "failed") { activity.GetTagItem(AgentKitTagNames.ErrorType).ShouldBe(outcome); }
        var logs = logger.Snapshot();
        logs.Select(static entry => entry.EventId.Id).ShouldBe([4080, 4081]);
        logs[0].Level.ShouldBe(LogLevel.Debug);
        logs[1].Level.ShouldBe(outcome switch { "cancelled" => LogLevel.Information, "failed" => LogLevel.Error, _ => LogLevel.Debug });
        logs[1].State["Outcome"].ShouldBe(outcome);
        foreach (var entry in logs)
        {
            entry.State["Operation"].ShouldBe(operation);
            entry.State["TenantId"].ShouldBe(request.Identity.TenantId);
            entry.State["PrincipalId"].ShouldBe(request.Identity.PrincipalId);
            entry.State["AgentId"].ShouldBe(request.AgentId);
            entry.State["SessionId"].ShouldBe(request.SessionId);
            entry.State["RunId"].ShouldBe(request.RunId);
            entry.State["SourceId"].ShouldBe(sourceId);
        }
        measured.Count.ShouldBe(2);
        measured.Single(item => item.Name == AgentKitMetricNames.ToolCatalogDiscoveryOperationCount).Value.ShouldBe(1);
        measured.Single(item => item.Name == AgentKitMetricNames.ToolCatalogDiscoveryOperationDuration).Value.ShouldBe(0.125);
        foreach (var (_, _, tags) in measured)
        {
            tags.Length.ShouldBe(2);
            tags.ShouldContain(new KeyValuePair<string, object?>(AgentKitTagNames.GenAiOperationName, operation));
            tags.ShouldContain(new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
        }
        Activity.Current.ShouldBeSameAs(parent);
    }

    [Fact]
    public void Constructor_WhenReferencesInvalid_RejectsBeforeObservation()
    {
        var reads = 0;
        var request = ToolCaptureTestData.Discovery();
        var clock = new CallbackTimestampTimeProvider(() => ++reads);
        var logger = new RecordingLogger<ToolCatalogDiscovery>();
        Should.Throw<ArgumentNullException>(() => new ToolDiscoveryObservation(AgentKitActivityNames.ToolCatalogDiscover, null!, clock, logger)).ParamName.ShouldBe("request");
        Should.Throw<ArgumentNullException>(() => new ToolDiscoveryObservation(AgentKitActivityNames.ToolCatalogDiscover, request, null!, logger)).ParamName.ShouldBe("timeProvider");
        Should.Throw<ArgumentNullException>(() => new ToolDiscoveryObservation(AgentKitActivityNames.ToolCatalogDiscover, request, clock, null!)).ParamName.ShouldBe("logger");
        Should.Throw<ArgumentNullException>(() => new ToolDiscoveryObservation(null!, request, clock, logger)).ParamName.ShouldBe("operation");
        Should.Throw<ArgumentException>(() => new ToolDiscoveryObservation("unknown", request, clock, logger)).ParamName.ShouldBe("operation");
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolDiscoveryObservation(AgentKitActivityNames.ToolCatalogDiscoverSource, request, clock, logger, default(ToolSourceId))).ParamName.ShouldBe("sourceId");
        Should.Throw<ArgumentException>(() => new ToolDiscoveryObservation(AgentKitActivityNames.ToolCatalogDiscoverSource, request, clock, logger)).ParamName.ShouldBe("sourceId");
        Should.Throw<ArgumentException>(() => new ToolDiscoveryObservation(AgentKitActivityNames.ToolCatalogDiscover, request, clock, logger, new ToolSourceId("source"))).ParamName.ShouldBe("sourceId");
        reads.ShouldBe(0);
        logger.Snapshot().ShouldBeEmpty();
    }

    [Fact]
    public void Complete_WhenOutcomeInvalid_RejectsBeforeMutatingTerminalState()
    {
        var logger = new RecordingLogger<ToolCatalogDiscovery>();
        using var observation = new ToolDiscoveryObservation(AgentKitActivityNames.ToolCatalogDiscover, ToolCaptureTestData.Discovery(), TimeProvider.System, logger);
        Should.Throw<ArgumentNullException>(() => observation.Complete(null!)).ParamName.ShouldBe("outcome");
        Should.Throw<ArgumentException>(() => observation.Complete("unbounded-content")).ParamName.ShouldBe("outcome");
        logger.Snapshot().Length.ShouldBe(1);
        observation.Complete("discovered");
        logger.Snapshot()[1].State["Outcome"].ShouldBe("discovered");
    }

    [Theory]
    [InlineData("sample")]
    [InlineData("start")]
    [InlineData("stop")]
    [InlineData("metrics")]
    [InlineData("logger")]
    public void Complete_WhenObserverThrows_ContainsFailureAndRestoresParent(string stage)
    {
        using var parent = new Activity("failing-discovery-observer").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => stage == "sample" && options.Parent.TraceId == parent.TraceId ? throw new InvalidOperationException("observer-secret") : ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => { if (stage == "start" && activity.TraceId == parent.TraceId) { throw new InvalidOperationException("observer-secret"); } },
            ActivityStopped = activity => { if (stage == "stop" && activity.TraceId == parent.TraceId) { throw new InvalidOperationException("observer-secret"); } },
        };
        ActivitySource.AddActivityListener(listener);
        using var metrics = ListenMetrics(parent, (_, _, _) => { if (stage == "metrics") { throw new InvalidOperationException("observer-secret"); } });
        var logger = new RecordingLogger<ToolCatalogDiscovery> { ThrowOnWrite = stage == "logger" };
        var observation = new ToolDiscoveryObservation(AgentKitActivityNames.ToolCatalogDiscover, ToolCaptureTestData.Discovery(), TimeProvider.System, logger);
        observation.Complete("discovered");
        observation.Dispose();
        Activity.Current.ShouldBeSameAs(parent);
        if (stage != "logger") { logger.Snapshot()[1].State["Outcome"].ShouldBe("discovered"); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dispose_WhenClockMissingOrReversed_OmitsDurationAndMarksIncompleteOperationFailed(bool reversed)
    {
        using var parent = new Activity("missing-discovery-clock").Start();
        using var listener = Listen(parent, _ => { });
        List<string> measured = [];
        using var metrics = ListenMetrics(parent, (name, _, _) => measured.Add(name));
        var ticks = 100L;
        var clock = new CallbackTimestampTimeProvider(() => reversed ? Interlocked.Decrement(ref ticks) : throw new InvalidOperationException("clock-secret"));
        var logger = new RecordingLogger<ToolCatalogDiscovery>();
        new ToolDiscoveryObservation(AgentKitActivityNames.ToolCatalogDiscover, ToolCaptureTestData.Discovery(), clock, logger).Dispose();
        logger.Snapshot()[1].State["Outcome"].ShouldBe("failed");
        measured.ShouldBe([AgentKitMetricNames.ToolCatalogDiscoveryOperationCount]);
    }

    private static ActivityListener Listen(Activity parent, Action<Activity> stopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => { if (activity.TraceId == parent.TraceId) { stopped(activity); } },
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static MeterListener ListenMetrics(Activity parent, Action<string, double, KeyValuePair<string, object?>[]> measured)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, observer) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name is AgentKitMetricNames.ToolCatalogDiscoveryOperationCount or AgentKitMetricNames.ToolCatalogDiscoveryOperationDuration) { observer.EnableMeasurementEvents(instrument); }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
        listener.Start();
        return listener;
        void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            if (Activity.Current?.TraceId == parent.TraceId) { measured(instrument.Name, value, tags.ToArray()); }
        }
    }
}
