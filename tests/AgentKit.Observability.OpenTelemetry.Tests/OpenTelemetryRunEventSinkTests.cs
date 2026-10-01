// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry.Tests;

/// <summary>Verifies run-event export, content capture policy, and failure isolation.</summary>
public sealed class OpenTelemetryRunEventSinkTests
{
    [Fact]
    public async Task PublishAsync_WhenTheEventIsContentFree_ExportsAStructuralSpanMetricAndLog()
    {
        var key = Key();
        var logger = new RecordingLogger<OpenTelemetryRunEventSink>();
        using var activities = Collect(key);
        using var metrics = new MetricCollector(AgentKitMetricNames.ObservationRunEventCount);
        var sink = Sink(key, logger: logger, configure: static options => options.Signals = OpenTelemetrySignalSet.Activities | OpenTelemetrySignalSet.Metrics | OpenTelemetrySignalSet.Logs);

        await sink.PublishAsync(RunEventTestData.Committed(sequence: 7), TestContext.Current.CancellationToken);

        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.OperationName.ShouldBe(AgentKitActivityNames.ObservationRunEventExport);
        span.Status.ShouldBe(ActivityStatusCode.Ok);
        span.GetTagItem(AgentKitTagNames.ObservationEventKind).ShouldBe("message_committed");
        span.GetTagItem(AgentKitTagNames.ObservationEventSequence).ShouldBe(7L);
        span.GetTagItem(AgentKitTagNames.ObservationExporterVersion).ShouldBe(1L);
        span.Tags.ContainsKey(AgentKitTagNames.ObservationContentValue).ShouldBeFalse();
        logger.Snapshot().ShouldHaveSingleItem().EventId.Id.ShouldBe(35000);
        var measurement = metrics.Snapshot().Where(item => Equals(item.Tags[AgentKitTagNames.ObservationExporterKey], key.Value)).ShouldHaveSingleItem();
        measurement.Tags[AgentKitTagNames.Outcome].ShouldBe("exported");
        measurement.Tags.Keys.Order().ShouldBe(
            new[] { AgentKitTagNames.ObservationExporterKey, AgentKitTagNames.ObservationEventKind, AgentKitTagNames.Outcome }.Order());
    }

    [Fact]
    public async Task PublishAsync_WhenOnlyTheLogSignalIsEnabled_StartsNoActivity()
    {
        var key = Key();
        using var activities = Collect(key);
        var sink = Sink(key, configure: static options => options.Signals = OpenTelemetrySignalSet.Logs);

        await sink.PublishAsync(RunEventTestData.Committed(), TestContext.Current.CancellationToken);

        activities.Snapshot().ShouldBeEmpty();
    }

    [Fact]
    public async Task PublishAsync_WhenContentCaptureIsOffByDefault_NeverInvokesTheRedactorOrExportsContent()
    {
        var key = Key();
        var redactor = new CallbackObservationRedactor(static (content, _) => new RedactedContent(content));
        using var activities = Collect(key);
        var sink = Sink(key, redactor);

        await sink.PublishAsync(RunEventTestData.Text("classified-model-output-4711"), TestContext.Current.CancellationToken);

        redactor.Calls.ShouldBe(0);
        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.Tags.ContainsKey(AgentKitTagNames.ObservationContentValue).ShouldBeFalse();
        SignalAssertions.ShouldNotContainContent(activities.Snapshot(), [], [], "classified-model-output-4711");
    }

    [Fact]
    public async Task PublishAsync_WhenCaptureIsEnabledAndTheRedactorReturnsContent_ExportsOnlyTheRedactedValue()
    {
        var key = Key();
        var redactor = new CallbackObservationRedactor(static (content, _) => new RedactedContent(new ObservationContent(
            content.Kind, DataClassification.Public, [.. "[redacted]"u8.ToArray()], new ContentFingerprint("sha256:redacted"))));
        using var activities = Collect(key);
        var sink = Sink(key, redactor, CaptureEnabled);

        await sink.PublishAsync(RunEventTestData.Text("classified-model-output-4711"), TestContext.Current.CancellationToken);

        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.GetTagItem(AgentKitTagNames.ObservationContentValue).ShouldBe("[redacted]");
        span.GetTagItem(AgentKitTagNames.ObservationContentKind).ShouldBe("ModelOutput");
        span.GetTagItem(AgentKitTagNames.ObservationContentClassification).ShouldBe("Public");
        span.GetTagItem(AgentKitTagNames.ObservationContentFingerprint).ShouldBe("sha256:redacted");
        SignalAssertions.ShouldNotContainContent(activities.Snapshot(), [], [], "classified-model-output-4711");
    }

    [Fact]
    public async Task PublishAsync_WhenTheRedactorThrows_OmitsTheContentButStillExportsTheEvent()
    {
        var key = Key();
        var logger = new RecordingLogger<OpenTelemetryRunEventSink>();
        var redactor = new CallbackObservationRedactor(static (_, _) => throw new InvalidOperationException("boom classified-model-output-4711"));
        using var activities = Collect(key);
        using var omitted = new MetricCollector(AgentKitMetricNames.ObservationContentOmittedCount);
        var sink = Sink(key, redactor, static options =>
        {
            CaptureEnabled(options);
            options.Signals |= OpenTelemetrySignalSet.Metrics | OpenTelemetrySignalSet.Logs;
        }, logger);

        await sink.PublishAsync(RunEventTestData.Text("classified-model-output-4711"), TestContext.Current.CancellationToken);

        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Ok);
        span.GetTagItem(AgentKitTagNames.ObservationContentOmittedReason).ShouldBe("redaction_failed");
        span.Tags.ContainsKey(AgentKitTagNames.ObservationContentValue).ShouldBeFalse();
        omitted.Snapshot().ShouldContain(item => Equals(item.Tags[AgentKitTagNames.ObservationExporterKey], key.Value));
        logger.Snapshot().ShouldContain(entry => entry.EventId.Id == 35002);
        SignalAssertions.ShouldNotContainContent(activities.Snapshot(), logger.Snapshot(), omitted.Snapshot(), "classified-model-output-4711");
    }

    [Fact]
    public async Task PublishAsync_WhenTheRedactorOmitsTheContent_RecordsTheOmissionReason()
    {
        var key = Key();
        using var activities = Collect(key);
        var sink = Sink(key, new CallbackObservationRedactor(static (_, _) => new ContentOmitted()), CaptureEnabled);

        await sink.PublishAsync(RunEventTestData.Text("x"), TestContext.Current.CancellationToken);

        activities.Snapshot().ShouldHaveSingleItem().GetTagItem(AgentKitTagNames.ObservationContentOmittedReason).ShouldBe("redactor_omitted");
    }

    [Fact]
    public async Task PublishAsync_WhenTheRedactorReturnsOversizedContent_OmitsItAsAPolicyViolation()
    {
        var key = Key();
        using var activities = Collect(key);
        var redactor = new CallbackObservationRedactor(static (content, policy) => new RedactedContent(new ObservationContent(
            content.Kind, DataClassification.Public, [.. new byte[policy.Bounds.MaximumBytesPerField + 1]], new ContentFingerprint("f"))));
        var sink = Sink(key, redactor, CaptureEnabled);

        await sink.PublishAsync(RunEventTestData.Text("x"), TestContext.Current.CancellationToken);

        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.GetTagItem(AgentKitTagNames.ObservationContentOmittedReason).ShouldBe("policy_violation");
        span.Tags.ContainsKey(AgentKitTagNames.ObservationContentValue).ShouldBeFalse();
    }

    [Fact]
    public async Task PublishAsync_WhenTheRedactorReturnsADisallowedClassification_OmitsItAsAPolicyViolation()
    {
        var key = Key();
        using var activities = Collect(key);
        var redactor = new CallbackObservationRedactor(static (content, _) => new RedactedContent(new ObservationContent(
            content.Kind, DataClassification.Restricted, [1], new ContentFingerprint("f"))));
        var sink = Sink(key, redactor, CaptureEnabled);

        await sink.PublishAsync(RunEventTestData.Text("x"), TestContext.Current.CancellationToken);

        activities.Snapshot().ShouldHaveSingleItem().GetTagItem(AgentKitTagNames.ObservationContentOmittedReason).ShouldBe("policy_violation");
    }

    [Fact]
    public async Task PublishAsync_WhenTheDeclaredClassificationIsNotAllowed_OmitsWithoutInvokingTheRedactor()
    {
        var key = Key();
        using var activities = Collect(key);
        var redactor = new CallbackObservationRedactor(static (content, _) => new RedactedContent(content));
        var sink = Sink(key, redactor, static options =>
        {
            options.ContentCapture = new ObservationContentCapturePolicy(true);
            options.ContentClassification = DataClassification.Confidential;
            options.AllowedClassifications = [DataClassification.Public];
        });

        await sink.PublishAsync(RunEventTestData.Text("classified-model-output-4711"), TestContext.Current.CancellationToken);

        redactor.Calls.ShouldBe(0);
        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.GetTagItem(AgentKitTagNames.ObservationContentOmittedReason).ShouldBe("classification_not_allowed");
        SignalAssertions.ShouldNotContainContent(activities.Snapshot(), [], [], "classified-model-output-4711");
    }

    [Fact]
    public async Task PublishAsync_WhenTheContentExceedsTheBound_PassesATruncatedPayloadToTheRedactor()
    {
        var key = Key();
        var observed = -1;
        var redactor = new CallbackObservationRedactor((content, _) =>
        {
            observed = content.Value.Length;
            return new ContentOmitted();
        });
        var sink = Sink(key, redactor, static options =>
        {
            CaptureEnabled(options);
            options.Bounds = new ObservationBounds(8);
        });

        await sink.PublishAsync(RunEventTestData.Text(new string('x', 1_000)), TestContext.Current.CancellationToken);

        observed.ShouldBe(8);
    }

    [Fact]
    public async Task PublishAsync_WhenAnExporterSignalFailsOnABestEffortSink_CompletesWithoutThrowing()
    {
        var key = Key();
        var logger = new RecordingLogger<OpenTelemetryRunEventSink> { ThrowOnWrite = true };
        var sink = Sink(key, logger: logger, configure: static options => options.Signals = OpenTelemetrySignalSet.Logs);

        await Should.NotThrowAsync(async () => await sink.PublishAsync(RunEventTestData.Committed(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishAsync_WhenAnExporterSignalFailsOnARequiredSink_SurfacesTheFailure()
    {
        var key = Key();
        var logger = new RecordingLogger<OpenTelemetryRunEventSink> { ThrowOnWrite = true };
        var sink = Sink(key, logger: logger, configure: static options =>
        {
            options.Signals = OpenTelemetrySignalSet.Logs;
            options.Delivery = new ObservationDeliveryPolicy(true, TimeSpan.FromSeconds(1));
        });

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await sink.PublishAsync(RunEventTestData.Committed(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishAsync_WhenTheTokenIsCancelled_PropagatesCancellation()
    {
        var sink = Sink(Key());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await sink.PublishAsync(RunEventTestData.Committed(), cts.Token));
    }

    [Fact]
    public async Task PublishAsync_WhenTheEventIsNull_ThrowsArgumentNullExceptionNamingRunEvent()
    {
        var exception = await Should.ThrowAsync<ArgumentNullException>(async () => await Sink(Key()).PublishAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("runEvent");
    }

    [Fact]
    public void Constructor_WhenCaptureIsEnabledWithTheOmissionOnlyRedactor_ThrowsInvalidOperationException()
    {
        var exception = Should.Throw<InvalidOperationException>(() => Sink(Key(), new OmissionOnlyObservationRedactor(), CaptureEnabled));

        exception.Message.ShouldContain("omission-only");
    }

    [Fact]
    public void Constructor_WhenADependencyIsNull_ThrowsArgumentNullExceptionNamingIt()
    {
        var snapshot = Snapshot(Key());
        var logger = new RecordingLogger<OpenTelemetryRunEventSink>();
        var redactor = new OmissionOnlyObservationRedactor();

        Should.Throw<ArgumentNullException>(() => new OpenTelemetryRunEventSink(null!, AgentKitDiagnostics.Metrics, redactor, logger, snapshot)).ParamName.ShouldBe("activities");
        Should.Throw<ArgumentNullException>(() => new OpenTelemetryRunEventSink(AgentKitDiagnostics.Activities, null!, redactor, logger, snapshot)).ParamName.ShouldBe("meter");
        Should.Throw<ArgumentNullException>(() => new OpenTelemetryRunEventSink(AgentKitDiagnostics.Activities, AgentKitDiagnostics.Metrics, null!, logger, snapshot)).ParamName.ShouldBe("redactor");
        Should.Throw<ArgumentNullException>(() => new OpenTelemetryRunEventSink(AgentKitDiagnostics.Activities, AgentKitDiagnostics.Metrics, redactor, null!, snapshot)).ParamName.ShouldBe("logger");
        Should.Throw<ArgumentNullException>(() => new OpenTelemetryRunEventSink(AgentKitDiagnostics.Activities, AgentKitDiagnostics.Metrics, redactor, logger, null!)).ParamName.ShouldBe("options");
    }

    private static void CaptureEnabled(OpenTelemetryObservationOptions options)
    {
        options.ContentCapture = new ObservationContentCapturePolicy(true);
        options.ContentClassification = DataClassification.Internal;
    }

    private static ObservationExporterKey Key() => new($"test-{Guid.NewGuid():N}");

    private static ActivityCollector Collect(ObservationExporterKey key) =>
        new(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => Equals(observation.GetTagItem(AgentKitTagNames.ObservationExporterKey), key.Value));

    private static OpenTelemetryObservationOptionsSnapshot Snapshot(
        ObservationExporterKey key,
        Action<OpenTelemetryObservationOptions>? configure = null)
    {
        var options = new OpenTelemetryObservationOptions();
        configure?.Invoke(options);
        return new OpenTelemetryObservationOptionsSnapshot(
            key,
            new ObservationExporterVersion(options.ExporterVersion),
            options.Signals,
            options.ContentCapture,
            options.ContentClassification,
            options.AllowedClassifications,
            options.Delivery,
            options.Bounds,
            options.AuditExporterProvidesDurableAcceptance);
    }

    private static OpenTelemetryRunEventSink Sink(
        ObservationExporterKey key,
        IObservationRedactor? redactor = null,
        Action<OpenTelemetryObservationOptions>? configure = null,
        ILogger<OpenTelemetryRunEventSink>? logger = null) =>
        new(
            AgentKitDiagnostics.Activities,
            AgentKitDiagnostics.Metrics,
            redactor ?? new CallbackObservationRedactor(static (_, _) => new ContentOmitted()),
            logger ?? new RecordingLogger<OpenTelemetryRunEventSink>(),
            Snapshot(key, configure));
}
