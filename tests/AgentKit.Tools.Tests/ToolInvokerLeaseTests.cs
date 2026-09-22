// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using AgentKit.Conformance;
using AgentKit.TestSupport;

public sealed class ToolInvokerLeaseTests: ToolInvokerLeaseConformanceTests
{
    protected override IToolInvokerLease CreateLease(ToolDescriptor tool, ToolSourceVersion sourceVersion, IToolInvoker invoker, Func<ValueTask> release) =>
        new ToolInvokerLease(tool, sourceVersion, invoker, release);

    [Fact]
    public void Constructor_WhenDependenciesAreInvalid_RejectsBeforeRelease()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var invoker = new CaptureTestToolInvoker();
        var calls = 0;
        ValueTask release() { calls++; return ValueTask.CompletedTask; }
        var version = new ToolSourceVersion("source-1");

        AssertExact<ArgumentNullException>(() => _ = new ToolInvokerLease(null!, version, invoker, release), "tool");
        AssertExact<ArgumentOutOfRangeException>(() => _ = new ToolInvokerLease(tool, default, invoker, release), "sourceVersion");
        AssertExact<ArgumentNullException>(() => _ = new ToolInvokerLease(tool, version, null!, release), "invoker");
        AssertExact<ArgumentNullException>(() => _ = new ToolInvokerLease(tool, version, invoker, null!), "release");
        calls.ShouldBe(0);
        invoker.Invocations.ShouldBe(0);
        invoker.Disposals.ShouldBe(0);
    }

    [Fact]
    public async Task InvokeAsync_WhenCalledDirectly_ThrowsBecauseACaptureMustNeverInvokeItsTools()
    {
        var invoker = new CaptureTestToolInvoker();
        var context = ToolCaptureTestData.InvocationContext();

        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await invoker.InvokeAsync(context, TestContext.Current.CancellationToken));

        invoker.Invocations.ShouldBe(1);
    }

    private static void AssertExact<TException>(Action action, string parameter) where TException : ArgumentException
    {
        var exception = Should.Throw<TException>(action);
        exception.GetType().ShouldBe(typeof(TException));
        exception.ParamName.ShouldBe(parameter);
    }
}
