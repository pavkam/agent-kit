// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks.Tests;



/// <summary>Verifies DefaultHookDispatcher behavior and contracts.</summary>
public sealed class DefaultHookDispatcherTests
{
    private static readonly HookPointId _point = new("test.point");
    private static Func<TestHook, TestHookEventArgs, HookDispatchScope, CancellationToken, Task> Invoker => static (hook, args, scope, ct) => hook.InvokeAsync(args, scope, ct);

    [Fact]
    public async Task DispatchAsync_WhenHooksNull_ThrowsArgumentNullException()
    {
        var dispatcher = new DefaultHookDispatcher();
        var exception = await Should.ThrowAsync<ArgumentNullException>(() => dispatcher.DispatchAsync(_point, null!, new TestHookEventArgs(), Invoker, HookDispatchScope.Root));
        exception.ParamName.ShouldBe("hooks");
    }

    [Fact]
    public async Task DispatchAsync_WhenArgsNull_ThrowsArgumentNullException()
    {
        var dispatcher = new DefaultHookDispatcher();
        var exception = await Should.ThrowAsync<ArgumentNullException>(() => dispatcher.DispatchAsync(_point, Array.Empty<TestHook>(), null!, Invoker, HookDispatchScope.Root));
        exception.ParamName.ShouldBe("args");
    }

    [Fact]
    public async Task DispatchAsync_WhenInvokeNull_ThrowsArgumentNullException()
    {
        var dispatcher = new DefaultHookDispatcher();
        var exception = await Should.ThrowAsync<ArgumentNullException>(() => dispatcher.DispatchAsync(_point, Array.Empty<TestHook>(), new TestHookEventArgs(), null!, HookDispatchScope.Root));
        exception.ParamName.ShouldBe("invoke");
    }

    [Fact]
    public async Task DispatchAsync_WhenScopeNull_ThrowsArgumentNullException()
    {
        var dispatcher = new DefaultHookDispatcher();
        var exception = await Should.ThrowAsync<ArgumentNullException>(() => dispatcher.DispatchAsync(_point, Array.Empty<TestHook>(), new TestHookEventArgs(), Invoker, null!));
        exception.ParamName.ShouldBe("scope");
    }

    [Fact]
    public async Task DispatchAsync_WhenNoOrderingConstraints_InvokesInRegistrationOrder()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[]
        {
            Hook("a"),
            Hook("b"),
            Hook("c")
        };
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
            new TestHook
            {
                Id = new HookId("c"),
                RunsBefore = [new HookId("a"), new HookId("b")]
            }
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
            new TestHook
            {
                Id = new HookId("c"),
                RunsBefore = [new HookId("a")]
            }
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
            new TestHook
            {
                Id = new HookId("a"),
                RunsAfter = [new HookId("c")]
            },
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
            new TestHook
            {
                Id = new HookId("a"),
                DependsOn = [new HookId("b")]
            },
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
        var hooks = new[]
        {
            new TestHook
            {
                Id = new HookId("a"),
                DependsOn = [new HookId("missing")]
            }
        };
        _ = await Should.ThrowAsync<HookCompositionException>(() => dispatcher.DispatchAsync(_point, hooks, new TestHookEventArgs(), Invoker, HookDispatchScope.Root, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DispatchAsync_WhenPriorityFirstDeclared_RunsFirst()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[]
        {
            Hook("a"),
            Hook("b"),
            new TestHook
            {
                Id = new HookId("c"),
                Priority = HookPriority.First
            }
        };
        var args = new TestHookEventArgs();
        await dispatcher.DispatchAsync(_point, hooks, args, Invoker, HookDispatchScope.Root, cancellationToken: TestContext.Current.CancellationToken);
        args.InvocationOrder[0].ShouldBe(new HookId("c"));
    }

    [Fact]
    public async Task DispatchAsync_WhenPriorityLastDeclared_RunsLast()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[]
        {
            new TestHook
            {
                Id = new HookId("c"),
                Priority = HookPriority.Last
            },
            Hook("a"),
            Hook("b")
        };
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
            new TestHook
            {
                Id = new HookId("a"),
                Priority = HookPriority.First
            },
            new TestHook
            {
                Id = new HookId("b"),
                Priority = HookPriority.First
            }
        };
        _ = await Should.ThrowAsync<HookCompositionException>(() => dispatcher.DispatchAsync(_point, hooks, new TestHookEventArgs(), Invoker, HookDispatchScope.Root, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DispatchAsync_WhenTwoHooksDeclareLast_ThrowsHookCompositionException()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[]
        {
            new TestHook
            {
                Id = new HookId("a"),
                Priority = HookPriority.Last
            },
            new TestHook
            {
                Id = new HookId("b"),
                Priority = HookPriority.Last
            }
        };
        _ = await Should.ThrowAsync<HookCompositionException>(() => dispatcher.DispatchAsync(_point, hooks, new TestHookEventArgs(), Invoker, HookDispatchScope.Root, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DispatchAsync_WhenDuplicateHookId_ThrowsHookCompositionException()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[]
        {
            Hook("a"),
            Hook("a")
        };
        _ = await Should.ThrowAsync<HookCompositionException>(() => dispatcher.DispatchAsync(_point, hooks, new TestHookEventArgs(), Invoker, HookDispatchScope.Root, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DispatchAsync_WhenOrderingFormsCycle_ThrowsHookCompositionException()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[]
        {
            new TestHook
            {
                Id = new HookId("a"),
                RunsAfter = [new HookId("b")]
            },
            new TestHook
            {
                Id = new HookId("b"),
                RunsAfter = [new HookId("a")]
            }
        };
        _ = await Should.ThrowAsync<HookCompositionException>(() => dispatcher.DispatchAsync(_point, hooks, new TestHookEventArgs(), Invoker, HookDispatchScope.Root, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DispatchAsync_WhenHookLeavesArgsInvalid_ThrowsHookValidationExceptionAndStopsDispatch()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[]
        {
            new TestHook
            {
                Id = new HookId("a"),
                OnInvoke = static (args, _, _) =>
                {
                    args.RejectPayload = true;
                    return Task.CompletedTask;
                }
            },
            Hook("b")
        };
        var args = new TestHookEventArgs();
        _ = await Should.ThrowAsync<HookValidationException>(() => dispatcher.DispatchAsync(_point, hooks, args, Invoker, HookDispatchScope.Root, cancellationToken: TestContext.Current.CancellationToken));
        args.InvocationOrder.ShouldBe([new HookId("a")]);
    }

    [Fact]
    public async Task DispatchAsync_WhenHookSetsShortCircuit_StopsInvokingLaterHooks()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[]
        {
            new TestHook
            {
                Id = new HookId("a"),
                OnInvoke = static (args, _, _) =>
                {
                    args.IsShortCircuited = true;
                    return Task.CompletedTask;
                }
            },
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
            new TestHook
            {
                Id = new HookId("a"),
                OnInvoke = static (_, _, _) => throw new InvalidOperationException("boom")
            },
            Hook("b")
        };
        var args = new TestHookEventArgs();
        _ = await Should.ThrowAsync<InvalidOperationException>(() => dispatcher.DispatchAsync(_point, hooks, args, Invoker, HookDispatchScope.Root, HookFailureMode.FailOperation, cancellationToken: TestContext.Current.CancellationToken));
        args.InvocationOrder.ShouldBe([new HookId("a")]);
    }

    [Fact]
    public async Task DispatchAsync_WhenFailureModeIsolate_SwallowsExceptionAndContinues()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[]
        {
            new TestHook
            {
                Id = new HookId("a"),
                OnInvoke = static (_, _, _) => throw new InvalidOperationException("boom")
            },
            Hook("b")
        };
        var args = new TestHookEventArgs();
        await dispatcher.DispatchAsync(_point, hooks, args, Invoker, HookDispatchScope.Root, HookFailureMode.Isolate, cancellationToken: TestContext.Current.CancellationToken);
        args.InvocationOrder.ShouldBe([new HookId("a"), new HookId("b")]);
    }

    [Fact]
    public async Task DispatchAsync_WhenIsolatedHookMutatesThenThrows_DoesNotLeakPartialMutationToLaterHooks()
    {
        // extensions-hooks-and-middleware.md: isolation "MUST NOT leak a partial mutation, short-circuit marker, or replacement value".
        var dispatcher = new DefaultHookDispatcher();
        var observedByB = "unset";
        var hooks = new[]
        {
            new TestHook
            {
                Id = new HookId("a"),
                OnInvoke = static (args, _, _) =>
                {
                    args.Payload = "partial-from-a";
                    throw new InvalidOperationException("boom after mutating");
                },
            },
            new TestHook { Id = new HookId("b"), OnInvoke = (args, _, _) => { observedByB = args.Payload; return Task.CompletedTask; } },
        };
        var args = new TestHookEventArgs { Payload = "original" };

        await dispatcher.DispatchAsync(_point, hooks, args, Invoker, HookDispatchScope.Root, HookFailureMode.Isolate, cancellationToken: TestContext.Current.CancellationToken);

        observedByB.ShouldBe("original");
        args.Payload.ShouldBe("original");
    }

    [Fact]
    public async Task DispatchAsync_WhenIsolatedHookShortCircuitsThenThrows_DoesNotLeaveShortCircuitMarkerSet()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[]
        {
            new TestHook
            {
                Id = new HookId("a"),
                OnInvoke = static (args, _, _) =>
                {
                    args.IsShortCircuited = true;
                    throw new InvalidOperationException("boom after short-circuit");
                },
            },
            Hook("b"),
        };
        var args = new TestHookEventArgs();

        await dispatcher.DispatchAsync(_point, hooks, args, Invoker, HookDispatchScope.Root, HookFailureMode.Isolate, cancellationToken: TestContext.Current.CancellationToken);

        // Either the marker is rolled back and b runs, or the dispatch honours the marker and b does not run; a set marker with b having run is incoherent.
        (args.IsShortCircuited && args.InvocationOrder.Contains(new HookId("b"))).ShouldBeFalse();
    }

    [Fact]
    public async Task DispatchAsync_WhenFailureModeIsolateAndHookThrowsOperationCanceled_PropagatesWithoutIsolating()
    {
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[]
        {
            new TestHook
            {
                Id = new HookId("a"),
                OnInvoke = static (_, _, ct) => throw new OperationCanceledException(ct)
            },
            Hook("b")
        };
        var args = new TestHookEventArgs();
        _ = await Should.ThrowAsync<OperationCanceledException>(() => dispatcher.DispatchAsync(_point, hooks, args, Invoker, HookDispatchScope.Root, HookFailureMode.Isolate, cancellationToken: TestContext.Current.CancellationToken));
        args.InvocationOrder.ShouldBe([new HookId("a")]);
    }

    [Fact]
    public async Task DispatchAsync_WhenIsolatedHookThrowsAfterInvalidMutation_RollsBackMutationAndContinues()
    {
        // Isolation restores the captured writable state, so an invalid mutation left by the failing hook never
        // reaches validation, later hooks, or the owning operation.
        var dispatcher = new DefaultHookDispatcher();
        var hooks = new[]
        {
            new TestHook
            {
                Id = new HookId("a"),
                OnInvoke = static (args, _, _) =>
                {
                    args.RejectPayload = true;
                    throw new InvalidOperationException("boom");
                }
            },
            Hook("b")
        };
        var args = new TestHookEventArgs();
        await dispatcher.DispatchAsync(_point, hooks, args, Invoker, HookDispatchScope.Root, HookFailureMode.Isolate, cancellationToken: TestContext.Current.CancellationToken);
        args.RejectPayload.ShouldBeFalse();
        args.InvocationOrder.ShouldBe([new HookId("a"), new HookId("b")]);
    }

    [Fact]
    public async Task DispatchAsync_WhenCancelledBeforeRemainingHooks_ThrowsOperationCanceledException()
    {
        var dispatcher = new DefaultHookDispatcher();
        using var cts = new CancellationTokenSource();
        var hooks = new[]
        {
            new TestHook
            {
                Id = new HookId("a"),
                OnInvoke = (_, _, _) =>
                {
                    cts.Cancel();
                    return Task.CompletedTask;
                }
            },
            Hook("b")
        };
        var args = new TestHookEventArgs();
        _ = await Should.ThrowAsync<OperationCanceledException>(() => dispatcher.DispatchAsync(_point, hooks, args, Invoker, HookDispatchScope.Root, cancellationToken: cts.Token));
        args.InvocationOrder.ShouldBe([new HookId("a")]);
    }

    [Fact]
    public async Task DispatchAsync_WhenReentrantWithinMaxDepth_Succeeds()
    {
        var dispatcher = new DefaultHookDispatcher();
        var innerHooks = new[]
        {
            Hook("inner")
        };
        var outerHooks = new[]
        {
            new TestHook
            {
                Id = new HookId("outer"),
                OnInvoke = async (args, scope, ct) => await dispatcher.DispatchAsync(_point, innerHooks, args, Invoker, scope, maxReentrantDepth: 2, cancellationToken: ct)
            }
        };
        var outerArgs = new TestHookEventArgs();
        await dispatcher.DispatchAsync(_point, outerHooks, outerArgs, Invoker, HookDispatchScope.Root, maxReentrantDepth: 2, cancellationToken: TestContext.Current.CancellationToken);
        outerArgs.InvocationOrder.ShouldBe([new HookId("outer"), new HookId("inner")]);
    }

    [Fact]
    public async Task DispatchAsync_WhenReentrantBeyondMaxDepth_ThrowsHookReentrancyException()
    {
        var dispatcher = new DefaultHookDispatcher();
        var innerHooks = new[]
        {
            Hook("inner")
        };
        var outerHooks = new[]
        {
            new TestHook
            {
                Id = new HookId("outer"),
                OnInvoke = async (args, scope, ct) => await dispatcher.DispatchAsync(_point, innerHooks, args, Invoker, scope, cancellationToken: ct)
            }
        };
        _ = await Should.ThrowAsync<HookReentrancyException>(() => dispatcher.DispatchAsync(_point, outerHooks, new TestHookEventArgs(), Invoker, HookDispatchScope.Root, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DispatchAsync_WhenEmptyHooks_CompletesWithoutInvoking()
    {
        var dispatcher = new DefaultHookDispatcher();
        var args = new TestHookEventArgs();
        await dispatcher.DispatchAsync(_point, Array.Empty<TestHook>(), args, Invoker, HookDispatchScope.Root, cancellationToken: TestContext.Current.CancellationToken);
        args.InvocationOrder.ShouldBeEmpty();
    }

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
    public async Task DispatchAsync_WhenCallerDepthExceedsHostCeiling_UsesHostCeiling()
    {
        var dispatcher = new DefaultHookDispatcher(Options.Create(new AgentHookOptions { MaximumInvocationDepth = 1 }));
        var innerHooks = new[]
        {
            Hook("inner")
        };
        var outerHooks = new[]
        {
            new TestHook
            {
                Id = new HookId("outer"),
                OnInvoke = async (args, scope, ct) => await dispatcher.DispatchAsync(_point, innerHooks, args, Invoker, scope, maxReentrantDepth: 3, cancellationToken: ct)
            }
        };
        var args = new TestHookEventArgs();

        _ = await Should.ThrowAsync<HookReentrancyException>(() => dispatcher.DispatchAsync(_point, outerHooks, args, Invoker, HookDispatchScope.Root, maxReentrantDepth: 3, cancellationToken: TestContext.Current.CancellationToken));

        args.InvocationOrder.ShouldBe([new HookId("outer")]);
    }

    [Fact]
    public async Task DispatchAsync_WhenHostCeilingAboveCallerDepth_UsesCallerDepth()
    {
        var dispatcher = new DefaultHookDispatcher(Options.Create(new AgentHookOptions { MaximumInvocationDepth = 8 }));
        var innerHooks = new[]
        {
            Hook("inner")
        };
        var outerHooks = new[]
        {
            new TestHook
            {
                Id = new HookId("outer"),
                OnInvoke = async (args, scope, ct) => await dispatcher.DispatchAsync(_point, innerHooks, args, Invoker, scope, maxReentrantDepth: 1, cancellationToken: ct)
            }
        };

        // The host would allow depth 8, but the caller's own limit of 1 still forbids the nested dispatch.
        _ = await Should.ThrowAsync<HookReentrancyException>(() => dispatcher.DispatchAsync(_point, outerHooks, new TestHookEventArgs(), Invoker, HookDispatchScope.Root, maxReentrantDepth: 1, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DispatchAsync_WhenHostCeilingPermitsCallerDepth_Succeeds()
    {
        var dispatcher = new DefaultHookDispatcher(Options.Create(new AgentHookOptions { MaximumInvocationDepth = 2 }));
        var innerHooks = new[]
        {
            Hook("inner")
        };
        var outerHooks = new[]
        {
            new TestHook
            {
                Id = new HookId("outer"),
                OnInvoke = async (args, scope, ct) => await dispatcher.DispatchAsync(_point, innerHooks, args, Invoker, scope, maxReentrantDepth: 2, cancellationToken: ct)
            }
        };
        var args = new TestHookEventArgs();

        await dispatcher.DispatchAsync(_point, outerHooks, args, Invoker, HookDispatchScope.Root, maxReentrantDepth: 2, cancellationToken: TestContext.Current.CancellationToken);

        args.InvocationOrder.ShouldBe([new HookId("outer"), new HookId("inner")]);
    }

    [Fact]
    public async Task DispatchAsync_WhenHostMinimumFailureModeIsStricter_EscalatesIsolation()
    {
        var dispatcher = new DefaultHookDispatcher(Options.Create(new AgentHookOptions { MinimumFailureMode = HookFailureMode.FailOperation }));
        var hooks = new[]
        {
            new TestHook
            {
                Id = new HookId("a"),
                OnInvoke = static (_, _, _) => throw new InvalidOperationException("boom")
            },
            Hook("b")
        };
        var args = new TestHookEventArgs();

        _ = await Should.ThrowAsync<InvalidOperationException>(() => dispatcher.DispatchAsync(_point, hooks, args, Invoker, HookDispatchScope.Root, HookFailureMode.Isolate, cancellationToken: TestContext.Current.CancellationToken));

        args.InvocationOrder.ShouldBe([new HookId("a")]);
    }

    [Fact]
    public async Task DispatchAsync_WhenCallerModeIsStricter_KeepsCallerMode()
    {
        var dispatcher = new DefaultHookDispatcher(Options.Create(new AgentHookOptions { MinimumFailureMode = HookFailureMode.Isolate }));
        var hooks = new[]
        {
            new TestHook
            {
                Id = new HookId("a"),
                OnInvoke = static (_, _, _) => throw new InvalidOperationException("boom")
            },
            Hook("b")
        };
        var args = new TestHookEventArgs();

        _ = await Should.ThrowAsync<InvalidOperationException>(() => dispatcher.DispatchAsync(_point, hooks, args, Invoker, HookDispatchScope.Root, HookFailureMode.FailOperation, cancellationToken: TestContext.Current.CancellationToken));

        args.InvocationOrder.ShouldBe([new HookId("a")]);
    }

    [Fact]
    public async Task DispatchAsync_WhenHostMinimumMatchesCallerIsolation_StillIsolates()
    {
        var dispatcher = new DefaultHookDispatcher(Options.Create(new AgentHookOptions { MinimumFailureMode = HookFailureMode.Isolate }));
        var hooks = new[]
        {
            new TestHook
            {
                Id = new HookId("a"),
                OnInvoke = static (_, _, _) => throw new InvalidOperationException("boom")
            },
            Hook("b")
        };
        var args = new TestHookEventArgs();

        await dispatcher.DispatchAsync(_point, hooks, args, Invoker, HookDispatchScope.Root, HookFailureMode.Isolate, cancellationToken: TestContext.Current.CancellationToken);

        args.InvocationOrder.ShouldBe([new HookId("a"), new HookId("b")]);
    }

    [Fact]
    public async Task DispatchAsync_WhenHostEscalatesFailureMode_LogsEscalationWithoutPayload()
    {
        const string payload = "secret-hook-payload-must-not-be-logged";
        var logger = new RecordingLogger<DefaultHookDispatcher>();
        var dispatcher = new DefaultHookDispatcher(Options.Create(new AgentHookOptions { MinimumFailureMode = HookFailureMode.FailOperation }), logger);
        var args = new TestHookEventArgs { Payload = payload };

        await dispatcher.DispatchAsync(_point, [Hook("a")], args, Invoker, HookDispatchScope.Root, HookFailureMode.Isolate, cancellationToken: TestContext.Current.CancellationToken);

        var logs = logger.Snapshot();
        var escalation = logs.Single(static log => log.EventId.Id == 8005);
        escalation.Level.ShouldBe(LogLevel.Debug);
        escalation.Category.ShouldBe(typeof(DefaultHookDispatcher).FullName);
        escalation.State["HookPoint"].ShouldBe(_point);
        escalation.State["HookInvocationId"].ShouldBe(args.InvocationId);
        escalation.State["RequestedFailureMode"].ShouldBe(HookFailureMode.Isolate);
        escalation.State["EffectiveFailureMode"].ShouldBe(HookFailureMode.FailOperation);
        foreach (var log in logs)
        {
            log.Message.ShouldNotContain(payload);
            log.State.Values.ShouldAllBe(value => value == null || !value.ToString()!.Contains(payload, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task DispatchAsync_WhenHostClampsDepth_LogsClampWithoutPayload()
    {
        const string payload = "secret-hook-payload-must-not-be-logged";
        var logger = new RecordingLogger<DefaultHookDispatcher>();
        var dispatcher = new DefaultHookDispatcher(Options.Create(new AgentHookOptions { MaximumInvocationDepth = 2 }), logger);
        var args = new TestHookEventArgs { Payload = payload };

        await dispatcher.DispatchAsync(_point, [Hook("a")], args, Invoker, HookDispatchScope.Root, maxReentrantDepth: 5, cancellationToken: TestContext.Current.CancellationToken);

        var logs = logger.Snapshot();
        var clamp = logs.Single(static log => log.EventId.Id == 8004);
        clamp.Level.ShouldBe(LogLevel.Debug);
        clamp.State["HookPoint"].ShouldBe(_point);
        clamp.State["HookInvocationId"].ShouldBe(args.InvocationId);
        clamp.State["RequestedDepth"].ShouldBe(5);
        clamp.State["EffectiveDepth"].ShouldBe(2);
        foreach (var log in logs)
        {
            log.Message.ShouldNotContain(payload);
            log.State.Values.ShouldAllBe(value => value == null || !value.ToString()!.Contains(payload, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task DispatchAsync_WhenHostCeilingsLeaveCallerUnchanged_DoesNotLogAdjustment()
    {
        var logger = new RecordingLogger<DefaultHookDispatcher>();
        var dispatcher = new DefaultHookDispatcher(Options.Create(new AgentHookOptions()), logger);

        await dispatcher.DispatchAsync(_point, [Hook("a")], new TestHookEventArgs(), Invoker, HookDispatchScope.Root, HookFailureMode.Isolate, cancellationToken: TestContext.Current.CancellationToken);

        logger.Snapshot().Where(static log => log.EventId.Id is 8004 or 8005).ShouldBeEmpty();
    }

    private static TestHook Hook(string id) => new()
    {
        Id = new HookId(id)
    };

    /// <summary>An <see cref="IOptions{TOptions}"/> whose value is null, to exercise the dispatcher's value guard.</summary>
    private sealed class NullValueOptions: IOptions<AgentHookOptions>
    {
        public AgentHookOptions Value => null!;
    }

    [Fact]
    public async Task DispatchAsync_WhenObserved_EmitsCorrelatedTerminalActivity()
    {
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity => stopped = activity,
        };
        ActivitySource.AddActivityListener(listener);
        var args = new TestHookEventArgs();
        var dispatcher = new DefaultHookDispatcher();
        await dispatcher.DispatchAsync<TestHook, TestHookEventArgs>(new HookPointId("test.observed"), [], args, static (_, _, _, _) => Task.CompletedTask, HookDispatchScope.Root, cancellationToken: TestContext.Current.CancellationToken);
        var activity = stopped.ShouldNotBeNull();
        activity.OperationName.ShouldBe(AgentKitActivityNames.HookDispatch);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.HookInvocationId).ShouldBe(args.InvocationId.ToString());
    }

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
}
