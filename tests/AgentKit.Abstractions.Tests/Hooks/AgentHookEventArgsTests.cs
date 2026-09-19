// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit;

public sealed class AgentHookEventArgsTests
{
    [Fact]
    public void Constructor_WhenDispatchNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new TestEventArgs(null!));

        exception.ParamName.ShouldBe("dispatch");
    }

    [Fact]
    public void Constructor_WhenValid_ExposesEveryProperty()
    {
        var dispatch = HookKernelTestData.Dispatch();

        var args = new TestEventArgs(dispatch);

        args.Point.ShouldBe(dispatch.Point);
        args.DispatchId.ShouldBe(dispatch.DispatchId);
        args.Correlation.ShouldBe(dispatch.Correlation);
        args.Timestamp.ShouldBe(dispatch.Timestamp);
        args.Deadline.ShouldBe(dispatch.Deadline);
    }

    [Fact]
    public void Validate_WhenNotOverridden_DoesNotThrow()
    {
        var args = new TestEventArgs(HookKernelTestData.Dispatch());

        Should.NotThrow(args.Validate);
    }

    [Fact]
    public void CaptureMutableState_WhenNotOverridden_ReturnsNull()
    {
        var args = new TestEventArgs(HookKernelTestData.Dispatch());

        args.CaptureMutableState().ShouldBeNull();
    }

    [Fact]
    public void RestoreMutableState_WhenNotOverridden_DoesNotThrow()
    {
        var args = new TestEventArgs(HookKernelTestData.Dispatch());

        Should.NotThrow(() => args.RestoreMutableState(null));
    }

    private sealed class TestEventArgs(HookDispatchMetadata dispatch): AgentHookEventArgs(dispatch);
}
