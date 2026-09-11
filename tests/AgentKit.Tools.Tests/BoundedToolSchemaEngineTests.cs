// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class BoundedToolSchemaEngineTests: Conformance.ToolSchemaEngineConformanceTests
{
    [Fact]
    public void Compile_WhenObserved_EmitsSafeCompilerAndValidationEvents()
    {
        var compilerLog = new RecordingLogger<BoundedToolSchemaEngine>();
        var validationLog = new RecordingLogger<CompiledToolSchema>();
        var engine = new BoundedToolSchemaEngine(new CallbackTimestampTimeProvider(() => 0), compilerLog, validationLog);
        using var parent = new Activity("schema-content-test").Start();
        List<Activity> stopped = [];
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => { if (activity.TraceId == parent.TraceId) { stopped.Add(activity); } },
        };
        ActivitySource.AddActivityListener(listener);
        var compiled = engine.Compile(ToolSchemaTestData.Schema(/*lang=json,strict*/ """{"description":"schema-secret","const":"argument-secret"}"""), ToolSchemaTestData.Limits, TestContext.Current.CancellationToken)
            .ShouldBeOfType<ToolSchemaCompiled>().Schema;
        compiled.Validate(ToolSchemaTestData.Instance("\"argument-secret\""), ToolSchemaTestData.Limits, TestContext.Current.CancellationToken).ShouldBe(ToolSchemaValidationResult.Valid);
        compiled.Validate(ToolSchemaTestData.Instance("\"bad-secret\""), ToolSchemaTestData.Limits, TestContext.Current.CancellationToken).ShouldBe(ToolSchemaValidationResult.Invalid);
        _ = engine.Compile(ToolSchemaTestData.Schema(/*lang=json,strict*/ """{"schema-secret":true}"""), ToolSchemaTestData.Limits, TestContext.Current.CancellationToken).ShouldBeOfType<ToolSchemaCompilationRejected>();
        stopped.Select(activity => activity.OperationName).ShouldBe([AgentKitActivityNames.ToolSchemaCompile, AgentKitActivityNames.ToolSchemaValidate, AgentKitActivityNames.ToolSchemaValidate, AgentKitActivityNames.ToolSchemaCompile]);
        stopped.Select(activity => activity.GetTagItem(AgentKitTagNames.Outcome)).ShouldBe(["accepted", "accepted", "invalid", "configuration_rejected"]);
        stopped.ShouldAllBe(activity => activity.ParentId == parent.Id);
        foreach (var entry in compilerLog.Snapshot().Concat(validationLog.Snapshot()))
        {
            entry.Message.ShouldNotContain("secret");
            entry.State.Values.ShouldAllBe(value => value == null || !value.ToString()!.Contains("secret", StringComparison.Ordinal));
        }
        foreach (var activity in stopped) { activity.TagObjects.ShouldAllBe(tag => tag.Value == null || !tag.Value.ToString()!.Contains("secret", StringComparison.Ordinal)); }
        Activity.Current.ShouldBeSameAs(parent);
    }

    [Fact]
    public void Compile_WhenObserversThrowOrCancel_ContainsFailuresAndPreservesCancellation()
    {
        var compilerLog = new RecordingLogger<BoundedToolSchemaEngine> { ThrowOnWrite = true };
        var validationLog = new RecordingLogger<CompiledToolSchema> { ThrowOnWrite = true };
        var engine = new BoundedToolSchemaEngine(new CallbackTimestampTimeProvider(() => throw new InvalidOperationException("clock-secret")), compilerLog, validationLog);
        using var parent = new Activity("schema-observer-test").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => { if (activity.TraceId == parent.TraceId) { throw new InvalidOperationException("listener-secret"); } },
            ActivityStopped = activity => { if (activity.TraceId == parent.TraceId) { throw new InvalidOperationException("listener-secret"); } },
        };
        ActivitySource.AddActivityListener(listener);
        var compiled = engine.Compile(ToolSchemaTestData.Schema("true"), ToolSchemaTestData.Limits, TestContext.Current.CancellationToken).ShouldBeOfType<ToolSchemaCompiled>().Schema;
        compiled.Validate(ToolSchemaTestData.Instance("null"), ToolSchemaTestData.Limits, TestContext.Current.CancellationToken).ShouldBe(ToolSchemaValidationResult.Valid);
        Activity.Current.ShouldBeSameAs(parent);
        using var cancellation = new CancellationTokenSource();
        var cancelling = new BoundedToolSchemaEngine(new CallbackTimestampTimeProvider(() => { cancellation.Cancel(); return 0; }), compilerLog, validationLog);
        Should.Throw<OperationCanceledException>(() => cancelling.Compile(ToolSchemaTestData.Schema("true"), ToolSchemaTestData.Limits, cancellation.Token)).CancellationToken.ShouldBe(cancellation.Token);
    }

    protected override IToolSchemaEngine CreateEngine()
    {
        using var host = new ServiceCollection().AddToolSchemaEngine().BuildServiceProvider();
        return host.GetRequiredService<IToolSchemaEngine>();
    }

    [Theory]
    [InlineData(/*lang=json,strict*/ """{"items":1}""")]
    [InlineData(/*lang=json,strict*/ """{"additionalProperties":[]}""")]
    [InlineData(/*lang=json,strict*/ """{"properties":[]}""")]
    [InlineData(/*lang=json,strict*/ """{"properties":{"x":null}}""")]
    [InlineData(/*lang=json,strict*/ """{"type":[]}""")]
    [InlineData(/*lang=json,strict*/ """{"type":["string","string"]}""")]
    [InlineData(/*lang=json,strict*/ """{"type":["string",1]}""")]
    [InlineData(/*lang=json,strict*/ """{"required":["x","x"]}""")]
    [InlineData(/*lang=json,strict*/ """{"required":[1]}""")]
    [InlineData(/*lang=json,strict*/ """{"required":false}""")]
    [InlineData(/*lang=json,strict*/ """{"enum":null}""")]
    [InlineData(/*lang=json,strict*/ """{"minimum":"1"}""")]
    [InlineData(/*lang=json,strict*/ """{"maximum":null}""")]
    [InlineData(/*lang=json,strict*/ """{"exclusiveMinimum":false}""")]
    [InlineData(/*lang=json,strict*/ """{"exclusiveMaximum":"0"}""")]
    [InlineData(/*lang=json,strict*/ """{"minLength":-1}""")]
    [InlineData(/*lang=json,strict*/ """{"maxItems":1.5}""")]
    [InlineData(/*lang=json,strict*/ """{"minProperties":"1"}""")]
    [InlineData(/*lang=json,strict*/ """{"uniqueItems":1}""")]
    [InlineData(/*lang=json,strict*/ """{"description":1}""")]
    [InlineData(/*lang=json,strict*/ """{"format":null}""")]
    [InlineData(/*lang=json,strict*/ """{"examples":{}}""")]
    [InlineData(/*lang=json,strict*/ """{"deprecated":"false"}""")]
    [InlineData(/*lang=json,strict*/ """{"readOnly":null}""")]
    [InlineData(/*lang=json,strict*/ """{"writeOnly":1}""")]
    [InlineData(/*lang=json,strict*/ """{"type":"object","type":"string"}""")]
    [InlineData(/*lang=json,strict*/ """{"default":{"duplicate":1,"duplicate":2}}""")]
    public void Compile_WhenSchemaMalformed_ReturnsConfigurationRejection(string json) =>
        CreateEngine().Compile(ToolSchemaTestData.Schema(json), ToolSchemaTestData.Limits, TestContext.Current.CancellationToken).ShouldBeOfType<ToolSchemaCompilationRejected>()
            .Reason.ShouldBe(ToolSchemaRejectionReason.InvalidSchema);

    [Theory]
    [InlineData(/*lang=json,strict*/ """{"$ref":"https://untrusted.test/schema"}""")]
    [InlineData(/*lang=json,strict*/ """{"$ref":"#"}""")]
    [InlineData(/*lang=json,strict*/ """{"pattern":"(a+)+$"}""")]
    [InlineData(/*lang=json,strict*/ """{"$vocabulary":{"https://untrusted.test/vocab":true}}""")]
    [InlineData(/*lang=json,strict*/ """{"properties":{"x":{"$schema":"https://json-schema.org/draft/2020-12/schema"}}}""")]
    [InlineData(/*lang=json,strict*/ """{"allOf":[{}]}""")]
    public void Compile_WhenVocabularyUnsupported_RejectsBeforeUse(string json) =>
        CreateEngine().Compile(ToolSchemaTestData.Schema(json), ToolSchemaTestData.Limits, TestContext.Current.CancellationToken).ShouldBeOfType<ToolSchemaCompilationRejected>()
            .Reason.ShouldBe(ToolSchemaRejectionReason.UnsupportedKeyword);

    [Fact]
    public void Compile_WhenBoundsExceeded_ReportsEveryResourceDimension()
    {
        var engine = CreateEngine(); var schema = ToolSchemaTestData.Schema(/*lang=json,strict*/ """{"properties":{"x":{"type":"string"}}}""");
        ToolSchemaLimits[] limits = [new(1, 64, 100, 10000), new(1000, 2, 100, 10000), new(1000, 64, 2, 10000), new(1000, 64, 100, 1)];
        foreach (var bound in limits)
        {
            engine.Compile(schema, bound, TestContext.Current.CancellationToken).ShouldBeOfType<ToolSchemaCompilationRejected>().Reason.ShouldBe(ToolSchemaRejectionReason.ResourceLimitExceeded);
        }
        var deep = ToolSchemaTestData.Schema("""{"default":""" + new string('[', 128) + "0" + new string(']', 128) + "}");
        engine.Compile(deep, new(10000, 256, 1000, 1000000), TestContext.Current.CancellationToken).ShouldBeOfType<ToolSchemaCompilationRejected>()
            .Reason.ShouldBe(ToolSchemaRejectionReason.ResourceLimitExceeded);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData(/*lang=json,strict*/ """{"$schema":"https://json-schema.org/draft/2020-12/schema","type":["number","null"]}""")]
    [InlineData(/*lang=json,strict*/ """{"required":[],"enum":[1,1],"minLength":1.0,"minItems":0,"maxProperties":1e1000}""")]
    [InlineData(/*lang=json,strict*/ """{"default":{"$ref":"data"},"examples":[{"pattern":"data"}],"format":"future-format","readOnly":true,"deprecated":false}""")]
    public void Compile_WhenSchemaSupported_RetainsUnchangedCanonicalData(string json)
    {
        var schema = ToolSchemaTestData.Schema(json);
        var compiled = CreateEngine().Compile(schema, ToolSchemaTestData.Limits, TestContext.Current.CancellationToken).ShouldBeOfType<ToolSchemaCompiled>().Schema;
        compiled.Schema.ShouldBeSameAs(schema);
    }

    [Fact]
    public void Constructor_WhenDependenciesNull_RejectsWithoutObservation()
    {
        var logger = NullLogger<BoundedToolSchemaEngine>.Instance; var compiled = NullLogger<CompiledToolSchema>.Instance;
        Should.Throw<ArgumentNullException>(() => new BoundedToolSchemaEngine(null!, logger, compiled)).ParamName.ShouldBe("clock");
        Should.Throw<ArgumentNullException>(() => new BoundedToolSchemaEngine(TimeProvider.System, null!, compiled)).ParamName.ShouldBe("logger");
        Should.Throw<ArgumentNullException>(() => new BoundedToolSchemaEngine(TimeProvider.System, logger, null!)).ParamName.ShouldBe("compiledLogger");
    }
}
