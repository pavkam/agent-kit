// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks.Tests;

public sealed class DefaultHookDispatcherTests
{
    private static readonly HookPointId _point = new("test.point");

    private static Func<TestHook, TestHookEventArgs, HookDispatchScope, CancellationToken, Task> Invoker =>
        static (hook, args, scope, ct) => hook.InvokeAsync(args, scope, ct);

    [Fact]
    public async Task DispatchAsync_WhenHooksNull_ThrowsArgumentNullException()
    {
        var dispatcher = new DefaultHookDispatcher();

        var exception = await Should.ThrowAsync<ArgumentNullException>(() => dispatcher.DispatchAsync(
            _point, null!, new TestHookEventArgs(), Invoker, HookDispatchScope.Root));

        exception.ParamName.ShouldBe("hooks");
    }

    [Fact]
    public async Task DispatchAsync_WhenArgsNull_ThrowsArgumentNullException()
    {
        var dispatcher = new DefaultHookDispatcher();

        var exception = await Should.ThrowAsync<ArgumentNullException>(() => dispatcher.DispatchAsync(
            _point, Array.Empty<TestHook>(), null!, Invoker, HookDispatchScope.Root));

        exception.ParamName.ShouldBe("args");
    }

    [Fact]
    public async Task DispatchAsync_WhenInvokeNull_ThrowsArgumentNullException()
    {
        var dispatcher = new DefaultHookDispatcher();

        var exception = await Should.ThrowAsync<ArgumentNullException>(() => dispatcher.DispatchAsync(
            _point, Array.Empty<TestHook>(), new TestHookEventArgs(), null!, HookDispatchScope.Root));

        exception.ParamName.ShouldBe("invoke");
    }

    [Fact]
    public async Task DispatchAsync_WhenScopeNull_ThrowsArgumentNullException()
    {
        var dispatcher = new DefaultHookDispatcher();

        var exception = await Should.ThrowAsync<ArgumentNullException>(() => dispatcher.DispatchAsync(
            _point, Array.Empty<TestHook>(), new TestHookEventArgs(), Invoker, null!));

        exception.ParamName.ShouldBe("scope");
    }

    [Fact]
    public async Task DispatchAsync_WhenNoOrderingConstraints_InvokesInRegistrationOrder()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[] { Hook("a"), Hook("b"), Hook("c") };
        var args = new TestHookEventArgs();

        await dispatcher.DispatchAsync(_point, hooks, args, Invoker, HookDispatchScope.Root, cancellationToken: TestContext.Current.CancellationToken);

        args.InvocationOrder.ShouldBe([new HookId("a"), new HookId("b"), new HookId("c")]);
    }

    [Fact]
    public async Task DispatchAsync_WhenRunsBeforeDeclared_RunsEarlier()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[]
        {
            Hook("a"),
            Hook("b"),
            new TestHook { Id = new HookId("c"), RunsBefore = [new HookId("a"), new HookId("b")] }
        };
        var args = new TestHookEventArgs();

        await dispatcher.DispatchAsync(_point, hooks, args, Invoker, HookDispatchScope.Root, cancellationToken: TestContext.Current.CancellationToken);

        args.InvocationOrder.ShouldBe([new HookId("c"), new HookId("a"), new HookId("b")]);
    }

    [Fact]
    public async Task DispatchAsync_WhenRunsBeforeDeclaredForOneTarget_PreservesRelativeOrderOfOthers()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[]
        {
            Hook("a"),
            Hook("b"),
            new TestHook { Id = new HookId("c"), RunsBefore = [new HookId("a")] }
        };
        var args = new TestHookEventArgs();

        await dispatcher.DispatchAsync(_point, hooks, args, Invoker, HookDispatchScope.Root, cancellationToken: TestContext.Current.CancellationToken);

        // c must run before a, but has no declared relationship to b, so b
        // (registered before c) keeps its earlier position.
        args.InvocationOrder.ShouldBe([new HookId("b"), new HookId("c"), new HookId("a")]);
    }

    [Fact]
    public async Task DispatchAsync_WhenRunsAfterDeclared_RunsLater()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[]
        {
            new TestHook { Id = new HookId("a"), RunsAfter = [new HookId("c")] },
            Hook("b"),
            Hook("c")
        };
        var args = new TestHookEventArgs();

        await dispatcher.DispatchAsync(_point, hooks, args, Invoker, HookDispatchScope.Root, cancellationToken: TestContext.Current.CancellationToken);

        args.InvocationOrder.ShouldBe([new HookId("b"), new HookId("c"), new HookId("a")]);
    }

    [Fact]
    public async Task DispatchAsync_WhenDependsOnDeclared_RunsAfterDependency()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[]
        {
            new TestHook { Id = new HookId("a"), DependsOn = [new HookId("b")] },
            Hook("b")
        };
        var args = new TestHookEventArgs();

        await dispatcher.DispatchAsync(_point, hooks, args, Invoker, HookDispatchScope.Root, cancellationToken: TestContext.Current.CancellationToken);

        args.InvocationOrder.ShouldBe([new HookId("b"), new HookId("a")]);
    }

    [Fact]
    public async Task DispatchAsync_WhenDependsOnMissing_ThrowsHookCompositionException()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[] { new TestHook { Id = new HookId("a"), DependsOn = [new HookId("missing")] } };

        _ = await Should.ThrowAsync<HookCompositionException>(() => dispatcher.DispatchAsync(
            _point, hooks, new TestHookEventArgs(), Invoker, HookDispatchScope.Root, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DispatchAsync_WhenPriorityFirstDeclared_RunsFirst()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[] { Hook("a"), Hook("b"), new TestHook { Id = new HookId("c"), Priority = HookPriority.First } };
        var args = new TestHookEventArgs();

        await dispatcher.DispatchAsync(_point, hooks, args, Invoker, HookDispatchScope.Root, cancellationToken: TestContext.Current.CancellationToken);

        args.InvocationOrder[0].ShouldBe(new HookId("c"));
    }

    [Fact]
    public async Task DispatchAsync_WhenPriorityLastDeclared_RunsLast()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[] { new TestHook { Id = new HookId("c"), Priority = HookPriority.Last }, Hook("a"), Hook("b") };
        var args = new TestHookEventArgs();

        await dispatcher.DispatchAsync(_point, hooks, args, Invoker, HookDispatchScope.Root, cancellationToken: TestContext.Current.CancellationToken);

        args.InvocationOrder[^1].ShouldBe(new HookId("c"));
    }

    [Fact]
    public async Task DispatchAsync_WhenTwoHooksDeclareFirst_ThrowsHookCompositionException()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[]
        {
            new TestHook { Id = new HookId("a"), Priority = HookPriority.First },
            new TestHook { Id = new HookId("b"), Priority = HookPriority.First }
        };

        _ = await Should.ThrowAsync<HookCompositionException>(() => dispatcher.DispatchAsync(
            _point, hooks, new TestHookEventArgs(), Invoker, HookDispatchScope.Root, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DispatchAsync_WhenTwoHooksDeclareLast_ThrowsHookCompositionException()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[]
        {
            new TestHook { Id = new HookId("a"), Priority = HookPriority.Last },
            new TestHook { Id = new HookId("b"), Priority = HookPriority.Last }
        };

        _ = await Should.ThrowAsync<HookCompositionException>(() => dispatcher.DispatchAsync(
            _point, hooks, new TestHookEventArgs(), Invoker, HookDispatchScope.Root, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DispatchAsync_WhenDuplicateHookId_ThrowsHookCompositionException()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[] { Hook("a"), Hook("a") };

        _ = await Should.ThrowAsync<HookCompositionException>(() => dispatcher.DispatchAsync(
            _point, hooks, new TestHookEventArgs(), Invoker, HookDispatchScope.Root, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DispatchAsync_WhenOrderingFormsCycle_ThrowsHookCompositionException()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[]
        {
            new TestHook { Id = new HookId("a"), RunsAfter = [new HookId("b")] },
            new TestHook { Id = new HookId("b"), RunsAfter = [new HookId("a")] }
        };

        _ = await Should.ThrowAsync<HookCompositionException>(() => dispatcher.DispatchAsync(
            _point, hooks, new TestHookEventArgs(), Invoker, HookDispatchScope.Root, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DispatchAsync_WhenHookLeavesArgsInvalid_ThrowsHookValidationExceptionAndStopsDispatch()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[]
        {
            new TestHook { Id = new HookId("a"), OnInvoke = static (args, _, _) => { args.RejectPayload = true; return Task.CompletedTask; } },
            Hook("b")
        };
        var args = new TestHookEventArgs();

        _ = await Should.ThrowAsync<HookValidationException>(() => dispatcher.DispatchAsync(
            _point, hooks, args, Invoker, HookDispatchScope.Root, cancellationToken: TestContext.Current.CancellationToken));

        args.InvocationOrder.ShouldBe([new HookId("a")]);
    }

    [Fact]
    public async Task DispatchAsync_WhenHookSetsShortCircuit_StopsInvokingLaterHooks()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[]
        {
            new TestHook { Id = new HookId("a"), OnInvoke = static (args, _, _) => { args.IsShortCircuited = true; return Task.CompletedTask; } },
            Hook("b"),
            Hook("c")
        };
        var args = new TestHookEventArgs();

        await dispatcher.DispatchAsync(_point, hooks, args, Invoker, HookDispatchScope.Root, cancellationToken: TestContext.Current.CancellationToken);

        args.InvocationOrder.ShouldBe([new HookId("a")]);
    }

    [Fact]
    public async Task DispatchAsync_WhenFailureModeFailOperation_PropagatesExceptionAndStopsDispatch()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[]
        {
            new TestHook { Id = new HookId("a"), OnInvoke = static (_, _, _) => throw new InvalidOperationException("boom") },
            Hook("b")
        };
        var args = new TestHookEventArgs();

        _ = await Should.ThrowAsync<InvalidOperationException>(() => dispatcher.DispatchAsync(
            _point, hooks, args, Invoker, HookDispatchScope.Root, HookFailureMode.FailOperation, cancellationToken: TestContext.Current.CancellationToken));

        args.InvocationOrder.ShouldBe([new HookId("a")]);
    }

    [Fact]
    public async Task DispatchAsync_WhenFailureModeIsolate_SwallowsExceptionAndContinues()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[]
        {
            new TestHook { Id = new HookId("a"), OnInvoke = static (_, _, _) => throw new InvalidOperationException("boom") },
            Hook("b")
        };
        var args = new TestHookEventArgs();

        await dispatcher.DispatchAsync(
            _point, hooks, args, Invoker, HookDispatchScope.Root, HookFailureMode.Isolate, cancellationToken: TestContext.Current.CancellationToken);

        args.InvocationOrder.ShouldBe([new HookId("a"), new HookId("b")]);
    }

    [Fact]
    public async Task DispatchAsync_WhenCancelledBeforeRemainingHooks_ThrowsOperationCanceledException()
    {
        var dispatcher = new DefaultHookDispatcher();
        using var cts = new CancellationTokenSource();
        var hooks = new[]
        {
            new TestHook { Id = new HookId("a"), OnInvoke = (_, _, _) => { cts.Cancel(); return Task.CompletedTask; } },
            Hook("b")
        };
        var args = new TestHookEventArgs();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => dispatcher.DispatchAsync(_point, hooks, args, Invoker, HookDispatchScope.Root, cancellationToken: cts.Token));

        args.InvocationOrder.ShouldBe([new HookId("a")]);
    }

    [Fact]
    public async Task DispatchAsync_WhenReentrantWithinMaxDepth_Succeeds()
    {
        var dispatcher = new DefaultHookDispatcher();
        var innerHooks = new[] { Hook("inner") };
        var outerHooks = new[]
        {
            new TestHook
            {
                Id = new HookId("outer"),
                OnInvoke = async (args, scope, ct) =>
                    await dispatcher.DispatchAsync(_point, innerHooks, args, Invoker, scope, maxReentrantDepth: 2, cancellationToken: ct)
            }
        };
        var outerArgs = new TestHookEventArgs();

        await dispatcher.DispatchAsync(
            _point, outerHooks, outerArgs, Invoker, HookDispatchScope.Root, maxReentrantDepth: 2, cancellationToken: TestContext.Current.CancellationToken);

        outerArgs.InvocationOrder.ShouldBe([new HookId("outer"), new HookId("inner")]);
    }

    [Fact]
    public async Task DispatchAsync_WhenReentrantBeyondMaxDepth_ThrowsHookReentrancyException()
    {
        var dispatcher = new DefaultHookDispatcher();
        var innerHooks = new[] { Hook("inner") };
        var outerHooks = new[]
        {
            new TestHook
            {
                Id = new HookId("outer"),
                OnInvoke = async (args, scope, ct) =>
                    await dispatcher.DispatchAsync(_point, innerHooks, args, Invoker, scope, cancellationToken: ct)
            }
        };

        _ = await Should.ThrowAsync<HookReentrancyException>(() => dispatcher.DispatchAsync(
            _point, outerHooks, new TestHookEventArgs(), Invoker, HookDispatchScope.Root, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DispatchAsync_WhenEmptyHooks_CompletesWithoutInvoking()
    {
        var dispatcher = new DefaultHookDispatcher();
        var args = new TestHookEventArgs();

        await dispatcher.DispatchAsync(
            _point, Array.Empty<TestHook>(), args, Invoker, HookDispatchScope.Root, cancellationToken: TestContext.Current.CancellationToken);

        args.InvocationOrder.ShouldBeEmpty();
    }

    private static TestHook Hook(string id) => new() { Id = new HookId(id) };
}
