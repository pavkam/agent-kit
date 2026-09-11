// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;
/// <summary>Verifies ToolCatalog behavior and contracts.</summary>
public sealed class ToolCatalogTests: Conformance.ToolCatalogConformanceTests<ToolCatalogConformanceFixture>
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
        var a = new FakeTool
        {
            Descriptor = TestFactory.Descriptor("dup")
        };
        var b = new FakeTool
        {
            Descriptor = TestFactory.Descriptor("dup")
        };
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
        var a = new FakeTool
        {
            Descriptor = TestFactory.Descriptor("a")
        };
        var b = new FakeTool
        {
            Descriptor = TestFactory.Descriptor("b")
        };
        var catalog = new ToolCatalog([a, b]);
        catalog.Descriptors.ShouldBe([a.Descriptor, b.Descriptor]);
    }

    [Fact]
    public void TryResolve_WhenToolRegistered_ReturnsTrueAndTool()
    {
        var tool = new FakeTool
        {
            Descriptor = TestFactory.Descriptor("a")
        };
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

    [Fact]
    public void Constructor_WhenCapturedDescriptorsDuplicate_ReadsEachGetterOnceAndRejects()
    {
        var duplicate = TestFactory.Descriptor("duplicate");
        var first = new ChangingDescriptorTool(duplicate, TestFactory.Descriptor("first-later"));
        var second = new ChangingDescriptorTool(duplicate, TestFactory.Descriptor("second-later"));
        var exception = Should.Throw<ArgumentException>(() => new ToolCatalog([first, second]));
        exception.ParamName.ShouldBe("tools");
        first.DescriptorReadCount.ShouldBe(1);
        second.DescriptorReadCount.ShouldBe(1);
    }

    [Fact]
    public void Catalog_WhenBorrowingDisposableTool_PreservesInstanceAndDoesNotDisposeIt()
    {
        var tool = new ChangingDescriptorTool(TestFactory.Descriptor("borrowed"));
        var catalog = new ToolCatalog([tool]);
        var found = catalog.TryResolve(new ToolId("borrowed"), out var resolved);
        found.ShouldBeTrue();
        resolved.ShouldBeSameAs(tool);
        tool.DisposeCount.ShouldBe(0);
    }

    [Fact]
    public void Constructor_WhenToolOrDescriptorNull_ThrowsBeforeReadingLaterDescriptor()
    {
        var later = new ChangingDescriptorTool(TestFactory.Descriptor("later"));
        Should.Throw<ArgumentNullException>(() => new ToolCatalog([null!, later])).ParamName.ShouldBe("tools");
        later.DescriptorReadCount.ShouldBe(0);
        var nullDescriptor = new NullDescriptorTool();
        Should.Throw<ArgumentNullException>(() => new ToolCatalog([nullDescriptor, later])).ParamName.ShouldBe("tools");
        nullDescriptor.DescriptorReadCount.ShouldBe(1);
        later.DescriptorReadCount.ShouldBe(0);
    }

    private sealed class ChangingDescriptorTool(params ToolDescriptor[] descriptors): ITool, IDisposable
    {
        public int DescriptorReadCount { get; private set; }
        public int InvocationCount { get; private set; }
        public int DisposeCount { get; private set; }
        public ToolInvocationRequest? ReceivedRequest { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }

        public ToolDescriptor Descriptor
        {
            get
            {
                var index = Math.Min(DescriptorReadCount, descriptors.Length - 1);
                DescriptorReadCount++;
                return descriptors[index];
            }
        }

        public Task<ToolInvocationResult> InvokeAsync(ToolInvocationRequest request, CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            ReceivedRequest = request;
            ReceivedToken = cancellationToken;
            return Task.FromResult(new ToolInvocationResult(new ToolCallOutcome(ToolCallOutcomeKind.Success, null, ExtensionData.Empty), []));
        }

        public void Dispose() => DisposeCount++;
    }

    private sealed class NullDescriptorTool: ITool
    {
        public int DescriptorReadCount { get; private set; }

        public ToolDescriptor Descriptor
        {
            get
            {
                DescriptorReadCount++;
                return null!;
            }
        }

        public Task<ToolInvocationResult> InvokeAsync(ToolInvocationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
