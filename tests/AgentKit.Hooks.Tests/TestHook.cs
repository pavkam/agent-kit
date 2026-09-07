// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks.Tests;

/// <summary>
/// A synthetic, fully scriptable <see cref="IHook"/> implementation used
/// only by this test project.
/// </summary>
internal sealed class TestHook: IHook
{
    public required HookId Id { get; init; }

    public HookPriority Priority { get; init; }

    public ImmutableArray<HookId> RunsBefore { get; init; } = [];

    public ImmutableArray<HookId> RunsAfter { get; init; } = [];

    public ImmutableArray<HookId> DependsOn { get; init; } = [];

    public Func<TestHookEventArgs, HookDispatchScope, CancellationToken, Task>? OnInvoke { get; init; }

    public async Task InvokeAsync(TestHookEventArgs args, HookDispatchScope scope, CancellationToken cancellationToken)
    {
        args.InvocationOrder.Add(Id);

        if (OnInvoke is not null)
        {
            await OnInvoke(args, scope, cancellationToken).ConfigureAwait(false);
        }
    }
}
