// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit.TestSupport;

public sealed class ToolDiscoverySelectionTests
{
    [Fact]
    public void Constructor_WhenMembershipComplete_RetainsExactRequestAndBorrowedBindingOrder()
    {
        var first = ToolCatalogMergeTestData.Candidate("first", "first");
        var second = ToolCatalogMergeTestData.Candidate("second", "second");
        var shared = ToolCatalogMergeTestData.Toolset("shared", [second.Source, first.Source], []);
        var a = new ToolProviderBinding(first.Source.SourceId, new CallbackToolProvider(first.Source.SourceId));
        var b = new ToolProviderBinding(second.Source.SourceId, new CallbackToolProvider(second.Source.SourceId));
        var request = ToolCatalogMergeTestData.Request([shared, first.Toolset]);
        var selection = new ToolDiscoverySelection(request, [shared, first.Toolset], [b, a]);
        selection.Request.ShouldBeSameAs(request);
        selection.Toolsets.ShouldBe([shared, first.Toolset]);
        selection.Providers.ShouldBe([b, a]);
        var copy = new ToolDiscoverySelection(request, [shared, first.Toolset], [b, a]);
        selection.ShouldBe(copy);
        selection.GetHashCode().ShouldBe(copy.GetHashCode());
        selection.Equals(null).ShouldBeFalse();
        selection.ShouldNotBe(new ToolDiscoverySelection(request, [shared, first.Toolset], [b, new(a.SourceId, a.Provider)]));
        new ToolDiscoverySelection(ToolCaptureTestData.Discovery(), [], []).Providers.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenMembershipOrOrderInvalid_RejectsExactParameter()
    {
        var first = ToolCatalogMergeTestData.Candidate("first", "first");
        var second = ToolCatalogMergeTestData.Candidate("second", "second");
        var a = new ToolProviderBinding(first.Source.SourceId, new CallbackToolProvider(first.Source.SourceId));
        var b = new ToolProviderBinding(second.Source.SourceId, new CallbackToolProvider(second.Source.SourceId));
        var request = ToolCatalogMergeTestData.Request([first.Toolset, second.Toolset]);
        Exact<ArgumentNullException>(() => _ = new ToolDiscoverySelection(null!, [], []), "request");
        Exact<ArgumentException>(() => _ = new ToolDiscoverySelection(request, default, [a, b]), "toolsets");
        Exact<ArgumentException>(() => _ = new ToolDiscoverySelection(request, [null!, second.Toolset], [a, b]), "toolsets");
        Exact<ArgumentException>(() => _ = new ToolDiscoverySelection(request, [first.Toolset, second.Toolset], default), "providers");
        Exact<ArgumentException>(() => _ = new ToolDiscoverySelection(request, [first.Toolset, second.Toolset], [a, null!]), "providers");
        Exact<ArgumentException>(() => _ = new ToolDiscoverySelection(request, [first.Toolset], [a]), "toolsets");
        Exact<ArgumentException>(() => _ = new ToolDiscoverySelection(request, [second.Toolset, first.Toolset], [b, a]), "toolsets");
        var wrongPolicy = ToolCatalogMergeTestData.Toolset("first", [first.Source], [], "wrong");
        Exact<ArgumentException>(() => _ = new ToolDiscoverySelection(request, [wrongPolicy, second.Toolset], [a, b]), "toolsets");
        Exact<ArgumentException>(() => _ = new ToolDiscoverySelection(request, [first.Toolset, second.Toolset], [a]), "providers");
        Exact<ArgumentException>(() => _ = new ToolDiscoverySelection(request, [first.Toolset, second.Toolset], [a, a]), "providers");
        Exact<ArgumentException>(() => _ = new ToolDiscoverySelection(request, [first.Toolset, second.Toolset], [b, a]), "providers");
        Exact<ArgumentException>(() => _ = new ToolDiscoverySelection(ToolCaptureTestData.Discovery(), [], [a]), "providers");
    }

    private static void Exact<TException>(Action action, string parameter) where TException : ArgumentException
    {
        var error = Should.Throw<TException>(action);
        error.GetType().ShouldBe(typeof(TException));
        error.ParamName.ShouldBe(parameter);
    }
}
