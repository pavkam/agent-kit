// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class CompiledToolSchemaTests: Conformance.CompiledToolSchemaConformanceTests
{
    protected override ICompiledToolSchema CompileSchema(JsonSchema schema, ToolSchemaLimits limits)
    {
        using var host = new ServiceCollection().AddToolSchemaEngine().BuildServiceProvider();
        return host.GetRequiredService<IToolSchemaEngine>().Compile(schema, limits, TestContext.Current.CancellationToken).ShouldBeOfType<ToolSchemaCompiled>().Schema;
    }

    public static IEnumerable<TheoryDataRow<string, string, string, bool>> RecordedCases()
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "TestData", "JsonSchema202012"), "*.json").Order(StringComparer.Ordinal))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var group in document.RootElement.EnumerateArray())
            {
                foreach (var test in group.GetProperty("tests").EnumerateArray())
                {
                    yield return new(Path.GetFileName(file) + ": " + group.GetProperty("description").GetString() + ": " + test.GetProperty("description").GetString(),
                        group.GetProperty("schema").GetRawText(), test.GetProperty("data").GetRawText(), test.GetProperty("valid").GetBoolean());
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(RecordedCases))]
    public void Validate_WhenRecordedDraft202012Case_PreservesSpecifiedResult(string description, string schema, string instance, bool expected)
    {
        using var host = new ServiceCollection().AddToolSchemaEngine().BuildServiceProvider();
        var compiled = host.GetRequiredService<IToolSchemaEngine>().Compile(ToolSchemaTestData.Schema(schema), ToolSchemaTestData.Limits, TestContext.Current.CancellationToken)
            .ShouldBeOfType<ToolSchemaCompiled>(description).Schema;
        compiled.Validate(ToolSchemaTestData.Instance(instance), ToolSchemaTestData.Limits, TestContext.Current.CancellationToken)
            .ShouldBe(expected ? ToolSchemaValidationResult.Valid : ToolSchemaValidationResult.Invalid, description);
    }

    [Fact]
    public void Compile_WhenFactoryArgumentsNull_RejectsBeforeProcessing()
    {
        var schema = ToolSchemaTestData.Schema("{}"); var limits = ToolSchemaTestData.Limits; var logger = NullLogger<CompiledToolSchema>.Instance;
        Should.Throw<ArgumentNullException>(() => CompiledToolSchema.Compile(null!, limits, TimeProvider.System, logger, TestContext.Current.CancellationToken)).ParamName.ShouldBe("schema");
        Should.Throw<ArgumentNullException>(() => CompiledToolSchema.Compile(schema, null!, TimeProvider.System, logger, TestContext.Current.CancellationToken)).ParamName.ShouldBe("limits");
        Should.Throw<ArgumentNullException>(() => CompiledToolSchema.Compile(schema, limits, null!, logger, TestContext.Current.CancellationToken)).ParamName.ShouldBe("clock");
        Should.Throw<ArgumentNullException>(() => CompiledToolSchema.Compile(schema, limits, TimeProvider.System, null!, TestContext.Current.CancellationToken)).ParamName.ShouldBe("logger");
    }

    [Theory]
    [InlineData("true", "null", true)]
    [InlineData("false", "null", false)]
    [InlineData(/*lang=json,strict*/ """{"type":"integer"}""", "1.0", true)]
    [InlineData(/*lang=json,strict*/ """{"type":"integer"}""", "1e10000000000000000000", true)]
    [InlineData(/*lang=json,strict*/ """{"type":"integer"}""", "1e-10000000000000000000", false)]
    [InlineData(/*lang=json,strict*/ """{"type":"integer"}""", "100e-2", true)]
    [InlineData(/*lang=json,strict*/ """{"type":"integer"}""", "101e-2", false)]
    [InlineData(/*lang=json,strict*/ """{"type":"integer"}""", "-0e-10000", true)]
    [InlineData(/*lang=json,strict*/ """{"type":["integer","null"],"minimum":1}""", "null", true)]
    [InlineData(/*lang=json,strict*/ """{"minimum":9007199254740993}""", "9007199254740992", false)]
    [InlineData(/*lang=json,strict*/ """{"minimum":9007199254740993}""", "9007199254740993", true)]
    [InlineData(/*lang=json,strict*/ """{"minimum":1.00000000000000000001}""", "1.000000000000000000001", false)]
    [InlineData(/*lang=json,strict*/ """{"maximum":-1e10000}""", "-2e10000", true)]
    [InlineData(/*lang=json,strict*/ """{"maximum":-1e10000}""", "-1e9999", false)]
    [InlineData(/*lang=json,strict*/ """{"exclusiveMinimum":1}""", "1.0", false)]
    [InlineData(/*lang=json,strict*/ """{"exclusiveMaximum":1}""", "1.0", false)]
    [InlineData(/*lang=json,strict*/ """{"exclusiveMinimum":1}""", "1.00000000000000000001", true)]
    [InlineData(/*lang=json,strict*/ """{"minLength":1,"maxLength":1}""", "\"😀\"", true)]
    [InlineData(/*lang=json,strict*/ """{"maxLength":1}""", "\"é\"", false)]
    [InlineData(/*lang=json,strict*/ """{"minLength":1}""", "\"\\u0000\"", true)]
    [InlineData(/*lang=json,strict*/ """{"minLength":1}""", "\"\"", false)]
    [InlineData(/*lang=json,strict*/ """{"minLength":1}""", "1", true)]
    [InlineData(/*lang=json,strict*/ """{"minItems":1,"maxItems":2,"items":{"type":"boolean"}}""", "[true,false]", true)]
    [InlineData(/*lang=json,strict*/ """{"items":false}""", "[]", true)]
    [InlineData(/*lang=json,strict*/ """{"items":false}""", "[null]", false)]
    [InlineData(/*lang=json,strict*/ """{"minItems":1}""", "[]", false)]
    [InlineData(/*lang=json,strict*/ """{"maxItems":1}""", "[1,2]", false)]
    [InlineData(/*lang=json,strict*/ """{"uniqueItems":true}""", "[1,1.0]", false)]
    [InlineData(/*lang=json,strict*/ """{"uniqueItems":true}""", /*lang=json,strict*/ """[{"a":1,"b":2},{"b":2.0,"a":1.0}]""", false)]
    [InlineData(/*lang=json,strict*/ """{"uniqueItems":true}""", "[[1,2],[2,1]]", true)]
    [InlineData(/*lang=json,strict*/ """{"uniqueItems":true}""", "[true,1]", true)]
    [InlineData(/*lang=json,strict*/ """{"enum":[{"a":[1],"b":null}]}""", /*lang=json,strict*/ """{"b":null,"a":[1.0]}""", true)]
    [InlineData(/*lang=json,strict*/ """{"const":1e10000}""", "10e9999", true)]
    [InlineData(/*lang=json,strict*/ """{"const":-0}""", "0.0", true)]
    [InlineData(/*lang=json,strict*/ """{"const":[1,2]}""", "[2,1]", false)]
    [InlineData(/*lang=json,strict*/ """{"const":{"a":1}}""", /*lang=json,strict*/ """{"a":1,"b":2}""", false)]
    [InlineData(/*lang=json,strict*/ """{"const":true}""", "false", false)]
    [InlineData(/*lang=json,strict*/ """{"const":"a"}""", "\"b\"", false)]
    [InlineData(/*lang=json,strict*/ """{"enum":[1,2]}""", "3", false)]
    [InlineData(/*lang=json,strict*/ """{"properties":{"x":{"type":"string"}},"additionalProperties":{"type":"integer"}}""", /*lang=json,strict*/ """{"x":"a","y":2}""", true)]
    [InlineData(/*lang=json,strict*/ """{"properties":{"x":{"type":"string"}},"additionalProperties":false}""", /*lang=json,strict*/ """{"x":"a","y":2}""", false)]
    [InlineData(/*lang=json,strict*/ """{"additionalProperties":false}""", /*lang=json,strict*/ """{"x":1}""", false)]
    [InlineData(/*lang=json,strict*/ """{"required":["x"],"minProperties":1,"maxProperties":1}""", /*lang=json,strict*/ """{"x":1}""", true)]
    [InlineData(/*lang=json,strict*/ """{"minProperties":1}""", "{}", false)]
    [InlineData(/*lang=json,strict*/ """{"maxProperties":1}""", /*lang=json,strict*/ """{"x":1,"y":2}""", false)]
    [InlineData(/*lang=json,strict*/ """{"format":"uuid","default":5}""", "\"not-a-uuid\"", true)]
    [InlineData("{}", /*lang=json,strict*/ """{"x":1,"x":2}""", false)]
    [InlineData("{}", /*lang=json,strict*/ """{"x":{"y":1,"y":2}}""", false)]
    [InlineData("{}", /*lang=json,strict*/ """{"x":1,"\u0078":2}""", false)]
    public void Validate_WhenCanonicalAssertionEvaluated_PreservesExactSemantics(string schema, string instance, bool valid)
    {
        var value = ToolSchemaTestData.Instance(instance);
        var raw = value.GetRawText();
        Compile(schema).Validate(value, ToolSchemaTestData.Limits, TestContext.Current.CancellationToken).ShouldBe(valid ? ToolSchemaValidationResult.Valid : ToolSchemaValidationResult.Invalid);
        value.GetRawText().ShouldBe(raw);
    }

    [Fact]
    public void Validate_WhenBoundsExceeded_DistinguishesUnknownValidity()
    {
        var compiled = Compile("{}"); var value = ToolSchemaTestData.Instance(/*lang=json,strict*/ """{"x":[1,2,3]}""");
        ToolSchemaLimits[] limits = [new(1, 64, 100, 10000), new(1000, 2, 100, 10000), new(1000, 64, 2, 10000), new(1000, 64, 100, 1)];
        foreach (var bound in limits) { compiled.Validate(value, bound, TestContext.Current.CancellationToken).ShouldBe(ToolSchemaValidationResult.ResourceLimitExceeded); }
        var unique = Compile(/*lang=json,strict*/ """{"uniqueItems":true}""");
        var array = ToolSchemaTestData.Instance("[" + string.Join(",", Enumerable.Range(0, 200)) + "]");
        unique.Validate(array, new(10000, 64, 1000, 10000), TestContext.Current.CancellationToken).ShouldBe(ToolSchemaValidationResult.ResourceLimitExceeded);
        Compile(/*lang=json,strict*/ """{"type":"integer"}""").Validate(ToolSchemaTestData.Instance("1e" + new string('9', 500)), new(1000, 64, 1000, 10000), TestContext.Current.CancellationToken)
            .ShouldBe(ToolSchemaValidationResult.ResourceLimitExceeded);
        unique.Validate(ToolSchemaTestData.Instance("[1,2]"), ToolSchemaTestData.Limits, TestContext.Current.CancellationToken).ShouldBe(ToolSchemaValidationResult.Valid);
    }

    [Fact]
    public void Validate_WhenAtByteDepthAndNodeBounds_AcceptsExactBoundary()
    {
        Compile("{}").Validate(ToolSchemaTestData.Instance(/*lang=json,strict*/ """{"x":1}"""), new(7, 2, 2, 10000), TestContext.Current.CancellationToken).ShouldBe(ToolSchemaValidationResult.Valid);
        Compile("{}").Validate(ToolSchemaTestData.Instance(/*lang=json,strict*/ "{ \"x\":1 }"), new(7, 2, 2, 10000), TestContext.Current.CancellationToken).ShouldBe(ToolSchemaValidationResult.ResourceLimitExceeded);
    }

    [Fact]
    public void Validate_WhenDocumentDisposed_ThrowsBeforeObservation()
    {
        var compiled = Compile("{}"); var document = JsonDocument.Parse("{}"); var value = document.RootElement; document.Dispose();
        _ = Should.Throw<ObjectDisposedException>(() => compiled.Validate(value, ToolSchemaTestData.Limits, TestContext.Current.CancellationToken));
    }

    private static ICompiledToolSchema Compile(string json) => new BoundedToolSchemaEngine(TimeProvider.System,
        NullLogger<BoundedToolSchemaEngine>.Instance, NullLogger<CompiledToolSchema>.Instance)
        .Compile(ToolSchemaTestData.Schema(json), ToolSchemaTestData.Limits, TestContext.Current.CancellationToken).ShouldBeOfType<ToolSchemaCompiled>().Schema;
}
