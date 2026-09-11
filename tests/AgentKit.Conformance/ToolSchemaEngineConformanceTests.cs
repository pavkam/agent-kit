// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using AgentKit.TestSupport;

/// <summary>Checks canonical schema capture, fail-closed complete preflight, and cancellation.</summary>
public abstract class ToolSchemaEngineConformanceTests
{
    /// <summary>Creates the subject through its public composition surface.</summary>
    /// <returns>A concurrently callable engine supporting the fixture's explicit object/type example.</returns>
    protected abstract IToolSchemaEngine CreateEngine();

    /// <summary>Successful compilation retains complete canonical evidence and does not apply defaults.</summary>
    [Fact]
    public void Compile_WhenSupported_RetainsExactSchemaProfileAndLimits()
    {
        var engine = CreateEngine();
        var schema = ToolSchemaTestData.Schema(/*lang=json,strict*/ """{"type":"object","properties":{"x":{"type":"integer","default":7}},"required":["x"],"additionalProperties":false}""");
        var compiled = engine.Compile(schema, ToolSchemaTestData.Limits, TestContext.Current.CancellationToken).ShouldBeOfType<ToolSchemaCompiled>().Schema;
        compiled.Schema.ShouldBe(schema); compiled.Profile.ShouldBe(engine.Profile); compiled.CompilationLimits.ShouldBe(ToolSchemaTestData.Limits);
    }

    /// <summary>Malformed branches must reject even when a later instance would never reach them.</summary>
    [Fact]
    public void Compile_WhenUnusedBranchMalformed_RejectsBeforeInstanceEvaluation()
    {
        var rejected = CreateEngine().Compile(ToolSchemaTestData.Schema(/*lang=json,strict*/ """{"properties":{"hidden":{"type":"not-a-type"}}}"""), ToolSchemaTestData.Limits, TestContext.Current.CancellationToken)
            .ShouldBeOfType<ToolSchemaCompilationRejected>();
        rejected.Reason.ShouldBe(ToolSchemaRejectionReason.InvalidSchema);
    }

    /// <summary>Unsupported dialects and unknown assertions never silently weaken canonical validation.</summary>
    [Fact]
    public void Compile_WhenCapabilitiesUnavailable_RejectsWithoutPartialHandle()
    {
        var engine = CreateEngine();
        var schema = new JsonSchema(new("urn:unsupported:contract-test"), ToolSchemaTestData.Instance("{}"));
        engine.Compile(schema, ToolSchemaTestData.Limits, TestContext.Current.CancellationToken).ShouldBeOfType<ToolSchemaCompilationRejected>().Reason.ShouldBe(ToolSchemaRejectionReason.UnsupportedDialect);
        engine.Compile(ToolSchemaTestData.Schema(/*lang=json,strict*/ """{"unregisteredAssertion":true}"""), ToolSchemaTestData.Limits, TestContext.Current.CancellationToken)
            .ShouldBeOfType<ToolSchemaCompilationRejected>().Reason.ShouldBe(ToolSchemaRejectionReason.UnsupportedKeyword);
    }

    /// <summary>Cancellation remains cancellation and null guards run before local processing.</summary>
    [Fact]
    public void Compile_WhenCancelledOrInvalid_PropagatesExactFailure()
    {
        var engine = CreateEngine();
        using var source = new CancellationTokenSource(); source.Cancel();
        var schema = ToolSchemaTestData.Schema("{}");
        Should.Throw<ArgumentNullException>(() => engine.Compile(null!, ToolSchemaTestData.Limits, source.Token)).ParamName.ShouldBe("schema");
        Should.Throw<ArgumentNullException>(() => engine.Compile(schema, null!, source.Token)).ParamName.ShouldBe("limits");
        Should.Throw<OperationCanceledException>(() => engine.Compile(schema, ToolSchemaTestData.Limits, source.Token)).CancellationToken.ShouldBe(source.Token);
    }

}
