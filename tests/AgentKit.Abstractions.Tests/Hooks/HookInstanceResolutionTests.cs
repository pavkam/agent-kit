// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit;

public sealed class HookInstanceResolutionTests
{
    [Fact]
    public void ResolvedConstructor_WhenHookIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new HookInstanceResolved<HookKernelTestData.ITestHook>(null!));
        exception.ParamName.ShouldBe("hook");
    }

    [Fact]
    public void ResolvedConstructor_WhenValid_RoundTripsHook()
    {
        var hook = new HookKernelTestData.TestHookImplementation();
        new HookInstanceResolved<HookKernelTestData.ITestHook>(hook).Hook.ShouldBe(hook);
    }

    [Fact]
    public void UnavailableConstructor_WhenReasonIsBlank_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new HookInstanceUnavailable<HookKernelTestData.ITestHook>(" "));
        exception.ParamName.ShouldBe("reason");
    }

    [Fact]
    public void UnavailableConstructor_WhenValid_RoundTripsReason() =>
        new HookInstanceUnavailable<HookKernelTestData.ITestHook>("not registered").Reason.ShouldBe("not registered");

    [Fact]
    public void Hierarchy_IsClosedToResolvedAndUnavailable()
    {
        var hook = new HookKernelTestData.TestHookImplementation();
        HookInstanceResolution<HookKernelTestData.ITestHook> resolved = new HookInstanceResolved<HookKernelTestData.ITestHook>(hook);
        HookInstanceResolution<HookKernelTestData.ITestHook> unavailable = new HookInstanceUnavailable<HookKernelTestData.ITestHook>("reason");

        _ = resolved.ShouldBeOfType<HookInstanceResolved<HookKernelTestData.ITestHook>>();
        _ = unavailable.ShouldBeOfType<HookInstanceUnavailable<HookKernelTestData.ITestHook>>();
    }
}
