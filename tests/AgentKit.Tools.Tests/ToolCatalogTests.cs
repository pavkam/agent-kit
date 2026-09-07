// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

public sealed class ToolCatalogTests
{
    [Fact]
    public void Constructor_WhenToolsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolCatalog(null!));

        exception.ParamName.ShouldBe("tools");
    }

    [Fact]
    public void Constructor_WhenDuplicateToolId_ThrowsArgumentException()
    {
        var a = new FakeTool { Descriptor = TestFactory.Descriptor("dup") };
        var b = new FakeTool { Descriptor = TestFactory.Descriptor("dup") };

        _ = Should.Throw<ArgumentException>(() => new ToolCatalog([a, b]));
    }

    [Fact]
    public void Constructor_WhenNoTools_ProducesEmptyCatalog()
    {
        var catalog = new ToolCatalog([]);

        catalog.Descriptors.ShouldBeEmpty();
    }

    [Fact]
    public void Descriptors_WhenToolsRegistered_ContainsEveryDescriptor()
    {
        var a = new FakeTool { Descriptor = TestFactory.Descriptor("a") };
        var b = new FakeTool { Descriptor = TestFactory.Descriptor("b") };

        var catalog = new ToolCatalog([a, b]);

        catalog.Descriptors.ShouldBe([a.Descriptor, b.Descriptor]);
    }

    [Fact]
    public void TryResolve_WhenToolRegistered_ReturnsTrueAndTool()
    {
        var tool = new FakeTool { Descriptor = TestFactory.Descriptor("a") };
        var catalog = new ToolCatalog([tool]);

        var found = catalog.TryResolve(new ToolId("a"), out var resolved);

        found.ShouldBeTrue();
        resolved.ShouldBeSameAs(tool);
    }

    [Fact]
    public void TryResolve_WhenToolNotRegistered_ReturnsFalse()
    {
        var catalog = new ToolCatalog([]);

        var found = catalog.TryResolve(new ToolId("missing"), out var resolved);

        found.ShouldBeFalse();
        resolved.ShouldBeNull();
    }
}
