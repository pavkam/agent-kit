// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit;

public sealed class IHookTests
{
    [Fact]
    public void Priority_WhenNotOverridden_DefaultsToNormal()
    {
        IHook hook = new MinimalHook();

        hook.Priority.ShouldBe(HookPriority.Normal);
    }

    [Fact]
    public void RunsBefore_WhenNotOverridden_DefaultsToEmpty()
    {
        IHook hook = new MinimalHook();

        hook.RunsBefore.ShouldBeEmpty();
    }

    [Fact]
    public void RunsAfter_WhenNotOverridden_DefaultsToEmpty()
    {
        IHook hook = new MinimalHook();

        hook.RunsAfter.ShouldBeEmpty();
    }

    [Fact]
    public void DependsOn_WhenNotOverridden_DefaultsToEmpty()
    {
        IHook hook = new MinimalHook();

        hook.DependsOn.ShouldBeEmpty();
    }

    private sealed class MinimalHook: IHook
    {
        public HookId Id { get; } = new("minimal");
    }
}
