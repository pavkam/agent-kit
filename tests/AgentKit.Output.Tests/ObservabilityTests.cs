// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output.Tests;

using System.Diagnostics.Metrics;

[Collection(OutputObservabilityGroup.Name)]
public sealed class ObservabilityTests
{
    [Fact]
    public async Task ProcessAsync_WhenObserved_EmitsContentFreeTerminalActivity()
    {
        const string protectedContent = "never-export-output-content";
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity => stopped = activity,
        };
        ActivitySource.AddActivityListener(listener);
        var definition = TestFactory.Definition(OutputMode.Text);
        var processor = new DefaultOutputProcessor(
            [],
            new StructuralOutputSchemaEngine(),
            new AgentOutputOptionsSnapshot(1024, 262144, 64, 4096, 64, 65536, 8, 1, true, false));

        _ = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, TestFactory.TextResponse(protectedContent)),
            TestContext.Current.CancellationToken);

        var activity = stopped.ShouldNotBeNull();
        activity.OperationName.ShouldBe(AgentKitActivityNames.OutputValidate);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.OutputDefinitionId).ShouldBe(definition.Id.ToString());
        activity.TagObjects.Select(static tag => tag.Value?.ToString()).ShouldNotContain(protectedContent);
    }

    [Fact]
    public void SchemaPreflight_WhenObserved_EmitsContentFreeSuccessfulActivityAndMetric()
    {
        const string protectedContent = "never-export-schema-content";
        Activity? stopped = null;
        long measurements = 0;
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity => stopped = activity,
        };
        ActivitySource.AddActivityListener(activityListener);
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == AgentKitMetricNames.OutputSchemaOperationCount)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((_, measurement, _, _) => Interlocked.Add(ref measurements, measurement));
        meterListener.Start();
        var engine = new StructuralOutputSchemaEngine();
        var schema = TestFactory.Schema($$"""{"description":"{{protectedContent}}","type":"string"}""");

        _ = engine.Preflight(
            new OutputSchemaPreflightRequest(schema, new OutputSchemaProcessingLimits(4096, 64, 4096)),
            TestContext.Current.CancellationToken).ShouldBeOfType<OutputSchemaPreflightAccepted>();

        var activity = stopped.ShouldNotBeNull();
        activity.OperationName.ShouldBe(AgentKitActivityNames.OutputSchemaPreflight);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.TagObjects.Select(static tag => tag.Value?.ToString()).ShouldNotContain(protectedContent);
        measurements.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void SchemaEvaluate_WhenCandidateIsInvalid_EmitsContentFreeErrorActivity()
    {
        const string protectedContent = "never-export-candidate-content";
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activities.Add,
        };
        ActivitySource.AddActivityListener(listener);
        var engine = new StructuralOutputSchemaEngine();
        var limits = new OutputSchemaProcessingLimits(4096, 64, 4096);
        var schema = TestFactory.Schema(/*lang=json,strict*/"""{"type":"integer"}""");
        var manifest = engine.Preflight(new OutputSchemaPreflightRequest(schema, limits), TestContext.Current.CancellationToken)
            .ShouldBeOfType<OutputSchemaPreflightAccepted>().Manifest;

        _ = engine.Evaluate(
            new OutputSchemaEvaluationRequest(
                schema,
                TestFactory.ParseJson($"\"{protectedContent}\""),
                manifest,
                limits,
                limits,
                4),
            TestContext.Current.CancellationToken).ShouldBeOfType<OutputSchemaCandidateInvalid>();

        var activity = activities.Single(item => item.OperationName == AgentKitActivityNames.OutputSchemaEvaluate);
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.TagObjects.Select(static tag => tag.Value?.ToString()).ShouldNotContain(protectedContent);
    }

    [Fact]
    public void SchemaPreflight_WhenCancelled_EmitsCancelledActivityAndPropagates()
    {
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity => stopped = activity,
        };
        ActivitySource.AddActivityListener(listener);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var engine = new StructuralOutputSchemaEngine();

        _ = Should.Throw<OperationCanceledException>(() => engine.Preflight(
            new OutputSchemaPreflightRequest(TestFactory.Schema("true"), new OutputSchemaProcessingLimits(4096, 64, 4096)),
            cancellation.Token));

        stopped.ShouldNotBeNull().Status.ShouldBe(ActivityStatusCode.Error);
    }

    [Fact]
    public void SchemaPreflight_WhenListenersAreDisabled_ReturnsSameSemanticOutcome()
    {
        var engine = new StructuralOutputSchemaEngine();

        var result = engine.Preflight(
            new OutputSchemaPreflightRequest(TestFactory.Schema("true"), new OutputSchemaProcessingLimits(4096, 64, 4096)),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<OutputSchemaPreflightAccepted>();
    }

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllDataAndRecorded;
}
