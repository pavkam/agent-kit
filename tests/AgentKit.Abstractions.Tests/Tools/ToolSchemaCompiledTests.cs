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
}
