// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks.Tests;

/// <summary>Construction and host-ceiling validation for <see cref="DefaultHookDispatcher"/>.</summary>
public sealed class DefaultHookDispatcherTests
{
    [Fact]
    public void Constructor_WhenOptionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultHookDispatcher((IOptions<AgentHookOptions>) null!));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenOptionsValueNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultHookDispatcher(new NullValueOptions()));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenMaximumInvocationDepthIsZero_ThrowsArgumentOutOfRangeException()
    {
        var options = Options.Create(new AgentHookOptions { MaximumInvocationDepth = 0 });

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DefaultHookDispatcher(options));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenMaximumInvocationDepthIsOne_DoesNotThrow()
    {
        var options = Options.Create(new AgentHookOptions { MaximumInvocationDepth = 1 });

        _ = Should.NotThrow(() => new DefaultHookDispatcher(options));
    }

    [Fact]
    public void Constructor_WhenMinimumFailureModeUndefined_ThrowsArgumentOutOfRangeException()
    {
        var options = Options.Create(new AgentHookOptions { MinimumFailureMode = (HookFailureMode) 42 });

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DefaultHookDispatcher(options));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenDefaultHookTimeoutZero_ThrowsArgumentOutOfRangeException()
    {
        var options = Options.Create(new AgentHookOptions { DefaultHookTimeout = TimeSpan.Zero });

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DefaultHookDispatcher(options));

        exception.ParamName.ShouldBe("options");
    }

    private sealed class NullValueOptions: IOptions<AgentHookOptions>
    {
        public AgentHookOptions Value => null!;
    }
}
