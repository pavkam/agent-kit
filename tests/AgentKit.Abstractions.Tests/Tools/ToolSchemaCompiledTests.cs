// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit.TestSupport;

public sealed class ToolSchemaCompiledTests
{
    [Fact]
    public void Constructor_WhenHandleNull_RejectsExactArgument() =>
        Should.Throw<ArgumentNullException>(() => new ToolSchemaCompiled(null!)).ParamName.ShouldBe("schema");
    [Fact]
    public void Constructor_WhenHandleSupplied_RetainsExactTypedHandle()
    {
        var handle = new CallbackCompiledToolSchema();
        new ToolSchemaCompiled(handle).Schema.ShouldBeSameAs(handle);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ToolSchemaCompiled(new CallbackCompiledToolSchema());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void Schema_WhenValidateIsConfigured_UsesTheConfiguredCallback()
    {
        var handle = new CallbackCompiledToolSchema();
        var expected = ToolSchemaValidationResult.Valid;
        handle.OnValidate = (_, _, _) => expected;
        var compiled = new ToolSchemaCompiled(handle);

        compiled.Schema.Validate(ToolSchemaTestData.Instance("null"), handle.CompilationLimits, TestContext.Current.CancellationToken).ShouldBe(expected);
    }

    [Fact]
    public void Schema_WhenValidateIsNotConfigured_ThrowsWithoutFabricatingASuccessfulResult()
    {
        var handle = new CallbackCompiledToolSchema();
        var compiled = new ToolSchemaCompiled(handle);

        _ = Should.Throw<InvalidOperationException>(
            () => compiled.Schema.Validate(ToolSchemaTestData.Instance("null"), handle.CompilationLimits, TestContext.Current.CancellationToken));
    }
}
