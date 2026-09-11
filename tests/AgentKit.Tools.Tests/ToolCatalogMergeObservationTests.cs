// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using System.Diagnostics.Metrics;

using AgentKit.TestSupport;

using Microsoft.Extensions.Logging;

public sealed class ToolCatalogMergeObservationTests
{
    [Theory]
    [InlineData(false, "selected")]
    [InlineData(false, "rejected")]
    [InlineData(false, "cancelled")]
    [InlineData(false, "failed")]
    [InlineData(true, "selected")]
    [InlineData(true, "rejected")]
    [InlineData(true, "cancelled")]
    [InlineData(true, "failed")]
    public void Complete_WhenObserved_RecordsOneSafeCorrelatedTerminalResult(bool policy, string outcome)
    {
        var request = ToolCaptureTestData.Discovery();
        var logger = new RecordingLogger<ToolCatalogMerger>();
        var ticks = 0L;
        var operation = policy ? AgentKitActivityNames.ToolCatalogMergePolicy : AgentKitActivityNames.ToolCatalogMerge;
        using var parent = new Activity("merge-observation-test").Start();
        Activity? observed = null;
        using var listener = Listen(parent, activity => observed = activity);
        List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> measured = [];
        using var metrics = ListenMetrics(parent, (name, value, tags) => measured.Add((name, value, tags)));
        var observation = new ToolCatalogMergeObservation(policy, request, new CallbackTimestampTimeProvider(() => Interlocked.Add(ref ticks, 125)), logger);
        observation.Complete(outcome);
        observation.Complete("failed");
        observation.Dispose();
        observation.Dispose();

        var activity = observed.ShouldNotBeNull();
        activity.OperationName.ShouldBe(operation);
        activity.ParentId.ShouldBe(parent.Id);
        activity.Status.ShouldBe(outcome == "selected" ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe(outcome);
        activity.GetTagItem(AgentKitTagNames.TenantId).ShouldBe(request.Identity.TenantId.Value);
        activity.GetTagItem(AgentKitTagNames.PrincipalId).ShouldBe(request.Identity.PrincipalId.Value);
        activity.GetTagItem(AgentKitTagNames.AgentId).ShouldBe(request.AgentId.ToString());
        activity.GetTagItem(AgentKitTagNames.SessionId).ShouldBe(request.SessionId.ToString());
        activity.GetTagItem(AgentKitTagNames.RunId).ShouldBe(request.RunId.ToString());
        if (outcome != "selected") { activity.GetTagItem(AgentKitTagNames.ErrorType).ShouldBe(outcome); }
        var logs = logger.Snapshot();
        logs.Select(static entry => entry.EventId.Id).ShouldBe([4060, 4061]);
        logs[0].Level.ShouldBe(LogLevel.Debug);
        logs[1].Level.ShouldBe(outcome switch { "selected" => LogLevel.Debug, "cancelled" => LogLevel.Information, "rejected" => LogLevel.Warning, _ => LogLevel.Error });
        logs[1].State["Outcome"].ShouldBe(outcome);
        foreach (var entry in logs)
        {
            entry.State["Operation"].ShouldBe(operation);
            entry.State["TenantId"].ShouldBe(request.Identity.TenantId);
            entry.State["PrincipalId"].ShouldBe(request.Identity.PrincipalId);
            entry.State["AgentId"].ShouldBe(request.AgentId);
            entry.State["SessionId"].ShouldBe(request.SessionId);
            entry.State["RunId"].ShouldBe(request.RunId);
        }
        measured.Count.ShouldBe(2);
        measured.Single(item => item.Name == AgentKitMetricNames.ToolCatalogMergeCount).Value.ShouldBe(1);
        measured.Single(item => item.Name == AgentKitMetricNames.ToolCatalogMergeDuration).Value.ShouldBe(0.125);
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
        var logger = new RecordingLogger<ToolCatalogMerger>();
        Should.Throw<ArgumentNullException>(() => new ToolCatalogMergeObservation(false, null!, clock, logger)).ParamName.ShouldBe("request");
        Should.Throw<ArgumentNullException>(() => new ToolCatalogMergeObservation(false, request, null!, logger)).ParamName.ShouldBe("timeProvider");
        Should.Throw<ArgumentNullException>(() => new ToolCatalogMergeObservation(false, request, clock, null!)).ParamName.ShouldBe("logger");
        reads.ShouldBe(0);
        logger.Snapshot().ShouldBeEmpty();
    }

    [Fact]
    public void Complete_WhenOutcomeInvalid_RejectsBeforeMutatingTerminalState()
    {
        var logger = new RecordingLogger<ToolCatalogMerger>();
        using var observation = new ToolCatalogMergeObservation(false, ToolCaptureTestData.Discovery(), TimeProvider.System, logger);
        Should.Throw<ArgumentNullException>(() => observation.Complete(null!)).ParamName.ShouldBe("outcome");
        Should.Throw<ArgumentException>(() => observation.Complete("unbounded-content")).ParamName.ShouldBe("outcome");
        logger.Snapshot().Length.ShouldBe(1);
        observation.Complete("selected");
        logger.Snapshot()[1].State["Outcome"].ShouldBe("selected");
    }

    [Theory]
    [InlineData("sample")]
    [InlineData("start")]
    [InlineData("stop")]
    [InlineData("metrics")]
    [InlineData("logger")]
    public void Complete_WhenObserverThrows_ContainsFailureAndRestoresParent(string stage)
    {
        using var parent = new Activity("failing-merge-observer").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => stage == "sample" && options.Parent.TraceId == parent.TraceId ? throw new InvalidOperationException("observer-secret") : ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => { if (stage == "start" && activity.TraceId == parent.TraceId) { throw new InvalidOperationException("observer-secret"); } },
            ActivityStopped = activity => { if (stage == "stop" && activity.TraceId == parent.TraceId) { throw new InvalidOperationException("observer-secret"); } },
        };
        ActivitySource.AddActivityListener(listener);
        using var metrics = ListenMetrics(parent, (_, _, _) => { if (stage == "metrics") { throw new InvalidOperationException("observer-secret"); } });
        var logger = new RecordingLogger<ToolCatalogMerger> { ThrowOnWrite = stage == "logger" };
        var observation = new ToolCatalogMergeObservation(false, ToolCaptureTestData.Discovery(), TimeProvider.System, logger);
        observation.Complete("selected");
        observation.Dispose();
        Activity.Current.ShouldBeSameAs(parent);
        if (stage != "logger") { logger.Snapshot()[1].State["Outcome"].ShouldBe("selected"); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dispose_WhenClockMissingOrReversed_OmitsDurationAndMarksIncompleteOperationFailed(bool reversed)
    {
        using var parent = new Activity("missing-merge-clock").Start();
        using var listener = Listen(parent, _ => { });
        List<string> measured = [];
        using var metrics = ListenMetrics(parent, (name, _, _) => measured.Add(name));
        var ticks = 100L;
        var clock = new CallbackTimestampTimeProvider(() => reversed ? Interlocked.Decrement(ref ticks) : throw new InvalidOperationException("clock-secret"));
        var logger = new RecordingLogger<ToolCatalogMerger>();
        new ToolCatalogMergeObservation(false, ToolCaptureTestData.Discovery(), clock, logger).Dispose();
        logger.Snapshot()[1].State["Outcome"].ShouldBe("failed");
        measured.ShouldBe([AgentKitMetricNames.ToolCatalogMergeCount]);
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
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name is AgentKitMetricNames.ToolCatalogMergeCount or AgentKitMetricNames.ToolCatalogMergeDuration) { observer.EnableMeasurementEvents(instrument); }
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
