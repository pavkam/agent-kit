// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using AgentKit.TestSupport;

public sealed class ToolProviderBindingsTests
{
    [Fact]
    public void Constructor_WhenBindingsAreExact_RetainsCapturedDescriptorsAndBorrowedInstances()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var snapshot = ToolCaptureTestData.Snapshot([tool]);
        var invoker = new CaptureTestToolInvoker();
        var bindings = new ToolProviderBindings(snapshot, ToolCaptureTestData.Bindings(tool, invoker));

        bindings.Snapshot.ShouldBeSameAs(snapshot);
        bindings.Entries.Count.ShouldBe(1);
        bindings.Entries[new(tool.Id, tool.Version)].Tool.ShouldBeSameAs(tool);
        bindings.Entries[new(tool.Id, tool.Version)].Invoker.ShouldBeSameAs(invoker);
        invoker.Invocations.ShouldBe(0);
        invoker.Disposals.ShouldBe(0);
    }

    [Fact]
    public void Constructor_WhenGraphInvalid_RejectsExactParameterBeforeTouchingInvokers()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var snapshot = ToolCaptureTestData.Snapshot([tool]);
        var invoker = new CaptureTestToolInvoker();
        var bindings = ToolCaptureTestData.Bindings(tool, invoker);
        Exact<ArgumentNullException>(() => _ = new ToolProviderBindings(null!, bindings), "snapshot");
        Exact<ArgumentNullException>(() => _ = new ToolProviderBindings(snapshot, null!), "invokers");
        Exact<ArgumentNullException>(() => _ = new ToolProviderBindings(snapshot, bindings.SetItem(new(tool.Id, tool.Version), null!)), "invokers");
        Exact<ArgumentOutOfRangeException>(() => _ = new ToolProviderBindings(snapshot, ImmutableDictionary<ToolIdentity, IToolInvoker>.Empty.Add(default, invoker)), "invokers");
        Exact<ArgumentException>(() => _ = new ToolProviderBindings(snapshot, []), "invokers");
        Exact<ArgumentException>(() => _ = new ToolProviderBindings(ToolCaptureTestData.Snapshot([]), bindings), "invokers");
        Exact<ArgumentException>(() => _ = new ToolProviderBindings(snapshot, ToolCaptureTestData.Bindings(ToolCaptureTestData.Descriptor(version: "2"), invoker)), "invokers");
        invoker.Invocations.ShouldBe(0);
        invoker.Disposals.ShouldBe(0);
    }

    [Fact]
    public void Constructor_WhenComparerWeakensIdentity_RejectsRatherThanChangingBindings()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var snapshot = ToolCaptureTestData.Snapshot([tool]);
        var invoker = new CaptureTestToolInvoker();
        var unequal = EqualityComparer<ToolIdentity>.Create(static (_, _) => false, static identity => identity.GetHashCode());
        var insensitive = EqualityComparer<ToolIdentity>.Create(static (a, b) => StringComparer.OrdinalIgnoreCase.Equals(a.Id.Value, b.Id.Value) && a.Version == b.Version,
            static value => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(value.Id.Value), value.Version));

        Exact<ArgumentException>(() => _ = new ToolProviderBindings(snapshot, ImmutableDictionary.Create<ToolIdentity, IToolInvoker>(unequal).Add(new(tool.Id, tool.Version), invoker).Add(new(tool.Id, tool.Version), invoker)), "invokers");
        Exact<ArgumentException>(() => _ = new ToolProviderBindings(snapshot, ImmutableDictionary.Create<ToolIdentity, IToolInvoker>(insensitive).Add(new(new ToolId("TOOL.READ"), tool.Version), invoker)), "invokers");
    }

    private static void Exact<TException>(Action action, string parameter) where TException : ArgumentException
    {
        var error = Should.Throw<TException>(action);
        error.GetType().ShouldBe(typeof(TException));
        error.ParamName.ShouldBe(parameter);
    }
}
