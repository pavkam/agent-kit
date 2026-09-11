// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using AgentKit.TestSupport;

/// <summary>Checks retained canonical validation through strongly typed compiled-handle factories.</summary>
public abstract class CompiledToolSchemaConformanceTests
{
    /// <summary>Creates a retained compiled handle through the implementation's public composition surface.</summary>
    /// <param name="schema">The exact owned canonical schema.</param>
    /// <param name="limits">Explicit compilation limits.</param>
    /// <returns>The handle whose validation contract is under test.</returns>
    protected abstract ICompiledToolSchema CompileSchema(JsonSchema schema, ToolSchemaLimits limits);

    /// <summary>Validation applies every canonical assertion without defaults, coercion, or instance mutation.</summary>
    [Fact]
    public void Validate_WhenCanonicalObjectRequired_RejectsMissingAndCoercedValues()
    {
        var schema = ToolSchemaTestData.Schema(/*lang=json,strict*/ """{"type":"object","properties":{"x":{"type":"integer","default":7}},"required":["x"],"additionalProperties":false}""");
        var compiled = CompileSchema(schema, ToolSchemaTestData.Limits);
        compiled.Validate(ToolSchemaTestData.Instance(/*lang=json,strict*/ """{"x":1}"""), ToolSchemaTestData.Limits, TestContext.Current.CancellationToken).ShouldBe(ToolSchemaValidationResult.Valid);
        var missing = ToolSchemaTestData.Instance("{}");
        compiled.Validate(missing, ToolSchemaTestData.Limits, TestContext.Current.CancellationToken).ShouldBe(ToolSchemaValidationResult.Invalid);
        missing.GetRawText().ShouldBe("{}");
        compiled.Validate(ToolSchemaTestData.Instance(/*lang=json,strict*/ """{"x":"1"}"""), ToolSchemaTestData.Limits, TestContext.Current.CancellationToken).ShouldBe(ToolSchemaValidationResult.Invalid);
    }

    /// <summary>Undefined/null arguments fail before evaluation and cancellation never becomes invalidity.</summary>
    [Fact]
    public void Validate_WhenCancelledOrInvalid_PropagatesExactFailure()
    {
        var compiled = CompileSchema(ToolSchemaTestData.Schema("{}"), ToolSchemaTestData.Limits);
        using var source = new CancellationTokenSource(); source.Cancel();
        Should.Throw<ArgumentException>(() => compiled.Validate(default, ToolSchemaTestData.Limits, source.Token)).ParamName.ShouldBe("instance");
        Should.Throw<ArgumentNullException>(() => compiled.Validate(ToolSchemaTestData.Instance("{}"), null!, source.Token)).ParamName.ShouldBe("limits");
        Should.Throw<OperationCanceledException>(() => compiled.Validate(ToolSchemaTestData.Instance("{}"), ToolSchemaTestData.Limits, source.Token)).CancellationToken.ShouldBe(source.Token);
    }

    /// <summary>Concurrent validations use fresh budgets without redirecting immutable canonical evidence.</summary>
    [Fact]
    public async Task Validate_WhenConcurrent_KeepsEveryEvaluationIndependent()
    {
        var compiled = CompileSchema(ToolSchemaTestData.Schema(/*lang=json,strict*/ """{"type":"integer"}"""), ToolSchemaTestData.Limits);
        await Task.WhenAll(Enumerable.Range(0, 24).Select(index => Task.Run(() =>
        {
            var valid = index % 2 == 0;
            compiled.Validate(ToolSchemaTestData.Instance(valid ? "1" : "1.5"), ToolSchemaTestData.Limits, TestContext.Current.CancellationToken)
                .ShouldBe(valid ? ToolSchemaValidationResult.Valid : ToolSchemaValidationResult.Invalid);
        }, TestContext.Current.CancellationToken)));
    }
}
