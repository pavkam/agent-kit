// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using System.Diagnostics.Metrics;

using AgentKit.TestSupport;

using Microsoft.Extensions.Logging;

public sealed class ToolSchemaObservationTests
{
    [Theory]
    [InlineData(AgentKitActivityNames.ToolSchemaCompile, "accepted")]
    [InlineData(AgentKitActivityNames.ToolSchemaCompile, "configuration_rejected")]
    [InlineData(AgentKitActivityNames.ToolSchemaCompile, "cancelled")]
    [InlineData(AgentKitActivityNames.ToolSchemaCompile, "failed")]
    [InlineData(AgentKitActivityNames.ToolSchemaValidate, "accepted")]
    [InlineData(AgentKitActivityNames.ToolSchemaValidate, "invalid")]
    [InlineData(AgentKitActivityNames.ToolSchemaValidate, "resource_limited")]
    [InlineData(AgentKitActivityNames.ToolSchemaValidate, "cancelled")]
    [InlineData(AgentKitActivityNames.ToolSchemaValidate, "failed")]
    public void Complete_WhenObserved_ReportsOneBoundedTruthfulTerminalResult(string operation, string outcome)
    {
        var logger = new RecordingLogger<BoundedToolSchemaEngine>(); var ticks = 0L;
        using var parent = new Activity("schema-observation-test").Start();
        Activity? stopped = null;
        using var listener = Listen(parent, activity => stopped = activity);
        List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> measured = [];
        using var metrics = ListenMetrics(parent, (name, value, tags) => measured.Add((name, value, tags)));
        var observation = new ToolSchemaObservation(operation, new CallbackTimestampTimeProvider(() => Interlocked.Add(ref ticks, 125)), logger);
        observation.Complete(outcome); observation.Complete("failed"); observation.Dispose(); observation.Dispose();
        var activity = stopped.ShouldNotBeNull();
        activity.OperationName.ShouldBe(operation); activity.ParentId.ShouldBe(parent.Id);
        activity.Status.ShouldBe(outcome == "accepted" ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe(outcome);
        Activity.Current.ShouldBeSameAs(parent);
        var logs = logger.Snapshot();
        logs.Select(entry => entry.EventId.Id).ShouldBe([4090, 4091]);
        logs[1].Level.ShouldBe(outcome == "accepted" ? LogLevel.Debug : outcome == "failed" ? LogLevel.Error : LogLevel.Information);
        logs[1].State.Keys.Order().ShouldBe(new List<string> { "{OriginalFormat}", "Operation", "Outcome" }.Order());
        logs[1].State["Operation"].ShouldBe(operation); logs[1].State["Outcome"].ShouldBe(outcome);
        measured.Count.ShouldBe(2);
        measured.Single(item => item.Name == AgentKitMetricNames.ToolSchemaOperationCount).Value.ShouldBe(1);
        measured.Single(item => item.Name == AgentKitMetricNames.ToolSchemaOperationDuration).Value.ShouldBe(0.125);
        foreach (var (_, _, tags) in measured)
        {
            tags.Length.ShouldBe(2);
            tags.ShouldContain(new KeyValuePair<string, object?>(AgentKitTagNames.GenAiOperationName, operation));
            tags.ShouldContain(new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
        }
    }

    [Fact]
    public void Constructor_WhenInvalid_RejectsBeforeAnyObservation()
    {
        var calls = 0; var clock = new CallbackTimestampTimeProvider(() => ++calls); var logger = new RecordingLogger<BoundedToolSchemaEngine>();
        Should.Throw<ArgumentNullException>(() => new ToolSchemaObservation(null!, clock, logger)).ParamName.ShouldBe("operation");
        Should.Throw<ArgumentException>(() => new ToolSchemaObservation("unknown", clock, logger)).ParamName.ShouldBe("operation");
        Should.Throw<ArgumentNullException>(() => new ToolSchemaObservation(AgentKitActivityNames.ToolSchemaCompile, null!, logger)).ParamName.ShouldBe("clock");
        Should.Throw<ArgumentNullException>(() => new ToolSchemaObservation(AgentKitActivityNames.ToolSchemaCompile, clock, null!)).ParamName.ShouldBe("logger");
        calls.ShouldBe(0); logger.Snapshot().ShouldBeEmpty();
        using var observation = new ToolSchemaObservation(AgentKitActivityNames.ToolSchemaCompile, clock, logger);
        Should.Throw<ArgumentNullException>(() => observation.Complete(null!)).ParamName.ShouldBe("outcome");
        Should.Throw<ArgumentException>(() => observation.Complete("unknown")).ParamName.ShouldBe("outcome");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dispose_WhenClockUnavailableOrReversed_OmitsDurationAndMarksUnfinishedFailure(bool reversed)
    {
        using var parent = new Activity("schema-clock-test").Start();
        using var listener = Listen(parent, _ => { });
        List<string> names = [];
        using var metrics = ListenMetrics(parent, (name, _, _) => names.Add(name));
        var ticks = 100L;
        var logger = new RecordingLogger<BoundedToolSchemaEngine>();
        new ToolSchemaObservation(AgentKitActivityNames.ToolSchemaCompile,
            new CallbackTimestampTimeProvider(() => reversed ? --ticks : throw new InvalidOperationException("clock-secret")), logger).Dispose();
        logger.Snapshot()[1].State["Outcome"].ShouldBe("failed");
        names.ShouldBe([AgentKitMetricNames.ToolSchemaOperationCount]);
    }

    private static ActivityListener Listen(Activity parent, Action<Activity> stopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => { if (activity.TraceId == parent.TraceId) { stopped(activity); } },
        };
        ActivitySource.AddActivityListener(listener); return listener;
    }
    private static MeterListener ListenMetrics(Activity parent, Action<string, double, KeyValuePair<string, object?>[]> measured)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, observer) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name is AgentKitMetricNames.ToolSchemaOperationCount or AgentKitMetricNames.ToolSchemaOperationDuration) { observer.EnableMeasurementEvents(instrument); }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
        listener.Start(); return listener;
        void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            if (Activity.Current?.TraceId == parent.TraceId) { measured(instrument.Name, value, tags.ToArray()); }
        }
    }
}
