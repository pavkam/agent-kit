// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry.Tests;

using AgentKit.Permissions;

/// <summary>Verifies audit export through the real permissions audit dispatcher.</summary>
public sealed class OpenTelemetrySecurityAuditSinkTests
{
    [Fact]
    public async Task DispatchAsync_WhenTheExporterSucceeds_WritesTheRecordAndEmitsASpanAndMetric()
    {
        var key = Key();
        var exporter = new RecordingAuditExporter();
        using var provider = Provider(key, exporter, static options => options.Signals = OpenTelemetrySignalSet.Audit | OpenTelemetrySignalSet.Activities | OpenTelemetrySignalSet.Metrics);
        using var activities = Collect(key);
        using var metrics = new MetricCollector(AgentKitMetricNames.ObservationAuditRecordCount);

        var result = await provider.GetRequiredService<ISecurityAuditDispatcher>().DispatchAsync(RunEventTestData.Audit(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SecurityAuditAccepted>();
        exporter.Records.ShouldHaveSingleItem().Id.ShouldBe(RunEventTestData.Audit().Id);
        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.OperationName.ShouldBe(AgentKitActivityNames.ObservationAuditExport);
        span.Status.ShouldBe(ActivityStatusCode.Ok);
        span.GetTagItem(AgentKitTagNames.SecurityAuditEventKind).ShouldBe("Decision");
        metrics.Snapshot().Where(item => Equals(item.Tags[AgentKitTagNames.ObservationExporterKey], key.Value))
            .ShouldHaveSingleItem().Tags[AgentKitTagNames.Outcome].ShouldBe("exported");
    }

    [Fact]
    public async Task DispatchAsync_WhenABestEffortExporterFails_IsolatesTheFailureInTheDispatcher()
    {
        var key = Key();
        var exporter = new RecordingAuditExporter { Failure = new InvalidOperationException("exporter down: classified-audit-detail") };
        using var provider = Provider(key, exporter, static options => options.Signals = OpenTelemetrySignalSet.Audit | OpenTelemetrySignalSet.Activities);
        using var activities = Collect(key);

        var result = await provider.GetRequiredService<ISecurityAuditDispatcher>().DispatchAsync(RunEventTestData.Audit(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SecurityAuditAccepted>();
        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Error);
        span.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("failed");
        SignalAssertions.ShouldNotContainContent(activities.Snapshot(), [], [], "classified-audit-detail");
    }

    [Fact]
    public async Task DispatchAsync_WhenARequiredDurableExporterFails_PreventsCleanDelivery()
    {
        var key = Key();
        var exporter = new RecordingAuditExporter { Failure = new InvalidOperationException("exporter down") };
        using var provider = Provider(key, exporter, static options =>
        {
            options.Signals = OpenTelemetrySignalSet.Audit;
            options.Delivery = new ObservationDeliveryPolicy(true, TimeSpan.FromSeconds(1));
            options.AuditExporterProvidesDurableAcceptance = true;
        });

        var result = await provider.GetRequiredService<ISecurityAuditDispatcher>().DispatchAsync(RunEventTestData.Audit(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SecurityAuditFailed>();
    }

    [Fact]
    public async Task DispatchAsync_WhenARequiredDurableExporterSucceeds_IsAccepted()
    {
        var key = Key();
        var exporter = new RecordingAuditExporter();
        using var provider = Provider(key, exporter, static options =>
        {
            options.Signals = OpenTelemetrySignalSet.Audit;
            options.Delivery = new ObservationDeliveryPolicy(true, TimeSpan.FromSeconds(1));
            options.AuditExporterProvidesDurableAcceptance = true;
        });

        var result = await provider.GetRequiredService<ISecurityAuditDispatcher>().DispatchAsync(RunEventTestData.Audit(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SecurityAuditAccepted>();
        _ = exporter.Records.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task WriteAsync_WhenTheTokenIsCancelled_PropagatesCancellationWithoutWriting()
    {
        var exporter = new RecordingAuditExporter();
        var sink = Sink(exporter);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await sink.WriteAsync(RunEventTestData.Audit(), cts.Token));

        exporter.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task WriteAsync_WhenTheRecordIsNull_ThrowsArgumentNullExceptionNamingRecord()
    {
        var exception = await Should.ThrowAsync<ArgumentNullException>(async () => await Sink(new RecordingAuditExporter()).WriteAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("record");
    }

    [Fact]
    public void Constructor_WhenADependencyIsNull_ThrowsArgumentNullExceptionNamingIt()
    {
        var snapshot = Snapshot(Key());
        var exporter = new RecordingAuditExporter();
        var logger = new RecordingLogger<OpenTelemetrySecurityAuditSink>();

        Should.Throw<ArgumentNullException>(() => new OpenTelemetrySecurityAuditSink(null!, AgentKitDiagnostics.Metrics, exporter, logger, snapshot)).ParamName.ShouldBe("activities");
        Should.Throw<ArgumentNullException>(() => new OpenTelemetrySecurityAuditSink(AgentKitDiagnostics.Activities, null!, exporter, logger, snapshot)).ParamName.ShouldBe("meter");
        Should.Throw<ArgumentNullException>(() => new OpenTelemetrySecurityAuditSink(AgentKitDiagnostics.Activities, AgentKitDiagnostics.Metrics, null!, logger, snapshot)).ParamName.ShouldBe("exporter");
        Should.Throw<ArgumentNullException>(() => new OpenTelemetrySecurityAuditSink(AgentKitDiagnostics.Activities, AgentKitDiagnostics.Metrics, exporter, null!, snapshot)).ParamName.ShouldBe("logger");
        Should.Throw<ArgumentNullException>(() => new OpenTelemetrySecurityAuditSink(AgentKitDiagnostics.Activities, AgentKitDiagnostics.Metrics, exporter, logger, null!)).ParamName.ShouldBe("options");
    }

    private static ObservationExporterKey Key() => new($"test-{Guid.NewGuid():N}");

    private static ServiceProvider Provider(
        ObservationExporterKey key,
        IOpenTelemetryAuditExporter exporter,
        Action<OpenTelemetryObservationOptions> configure,
        SecurityAuditDelivery auditDelivery = SecurityAuditDelivery.BestEffort)
    {
        var services = new ServiceCollection();
        _ = services.AddAgentPermissions(options =>
        {
            options.PolicySnapshot = TestSecurityEvidence.PolicySnapshot;
            options.AuditDelivery = auditDelivery;
        });
        _ = services.AddSingleton(exporter);
        _ = services.AddOpenTelemetryObservability(key, configure);
        return services.BuildServiceProvider();
    }

    private static ActivityCollector Collect(ObservationExporterKey key) =>
        new(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => Equals(observation.GetTagItem(AgentKitTagNames.ObservationExporterKey), key.Value));

    private static OpenTelemetryObservationOptionsSnapshot Snapshot(ObservationExporterKey key) =>
        new(
            key,
            new ObservationExporterVersion(1),
            OpenTelemetrySignalSet.Audit,
            new ObservationContentCapturePolicy(),
            DataClassification.Confidential,
            [DataClassification.Public],
            new ObservationDeliveryPolicy(),
            new ObservationBounds(64),
            AuditExporterProvidesDurableAcceptance: false);

    private static OpenTelemetrySecurityAuditSink Sink(IOpenTelemetryAuditExporter exporter) =>
        new(
            AgentKitDiagnostics.Activities,
            AgentKitDiagnostics.Metrics,
            exporter,
            new RecordingLogger<OpenTelemetrySecurityAuditSink>(),
            Snapshot(Key()));
}
