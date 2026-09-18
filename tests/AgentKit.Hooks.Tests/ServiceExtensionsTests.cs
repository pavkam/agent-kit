// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks.Tests;

public sealed class ServiceExtensionsTests
{
    private static readonly HookPointId _point = new("test.point");

    [Fact]
    public void AddRunStartedHook_WhenCalled_RegistersTheHookAndTheDispatcherOnce()
    {
        var services = new ServiceCollection();

        _ = services.AddRunStartedHook<TestRunStartedHook>().AddRunStartedHook<TestRunStartedHook>();

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IHookDispatcher>().ShouldBeOfType<DefaultHookDispatcher>();
        _ = provider.GetServices<IRunStartedHook>().ShouldHaveSingleItem().ShouldBeOfType<TestRunStartedHook>();
    }

    [Fact]
    public void AddBeforeModelRequestHook_WhenCalled_RegistersAdditively()
    {
        var services = new ServiceCollection();

        _ = services.AddBeforeModelRequestHook<TestBeforeModelRequestHook>().AddBeforeModelRequestHook<OtherBeforeModelRequestHook>();

        using var provider = services.BuildServiceProvider();
        provider.GetServices<IBeforeModelRequestHook>().Select(static h => h.GetType()).ShouldBe([typeof(TestBeforeModelRequestHook), typeof(OtherBeforeModelRequestHook)]);
    }

    [Fact]
    public void AddBeforeToolInvocationHook_WhenCalled_RegistersTheHook()
    {
        var services = new ServiceCollection();

        _ = services.AddBeforeToolInvocationHook<TestBeforeToolInvocationHook>();

        using var provider = services.BuildServiceProvider();
        _ = provider.GetServices<IBeforeToolInvocationHook>().ShouldHaveSingleItem().ShouldBeOfType<TestBeforeToolInvocationHook>();
    }

    [Fact]
    public void AddPointHooks_WhenServicesNull_ThrowArgumentNullException()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(services.AddRunStartedHook<TestRunStartedHook>).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(services.AddBeforeModelRequestHook<TestBeforeModelRequestHook>).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(services.AddBeforeToolInvocationHook<TestBeforeToolInvocationHook>).ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddAgentHooks_WhenServicesNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        var exception = Should.Throw<ArgumentNullException>(() => services.AddAgentHooks());

        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddAgentHooks_WhenCalled_RegistersDefaultDispatcher()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentHooks();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<IHookDispatcher>().ShouldBeOfType<DefaultHookDispatcher>();
    }

    [Fact]
    public void AddAgentHooks_WhenCalledTwice_KeepsFirstRegistration()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentHooks();
        _ = services.AddAgentHooks();
        using var provider = services.BuildServiceProvider();

        provider.GetServices<IHookDispatcher>().Count().ShouldBe(1);
    }

    [Fact]
    public void AddAgentHooks_WhenConfigureOmitted_UsesDocumentedDefaults()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentHooks();
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<AgentHookOptions>>().Value;
        options.MaximumInvocationDepth.ShouldBe(8);
        options.MinimumFailureMode.ShouldBe(HookFailureMode.Isolate);
    }

    [Fact]
    public void AddAgentHooks_WhenConfigureSupplied_AppliesConfiguration()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentHooks(static options =>
        {
            options.MaximumInvocationDepth = 3;
            options.MinimumFailureMode = HookFailureMode.FailOperation;
        });
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<AgentHookOptions>>().Value;
        options.MaximumInvocationDepth.ShouldBe(3);
        options.MinimumFailureMode.ShouldBe(HookFailureMode.FailOperation);
    }

    [Fact]
    public void AddAgentHooks_WhenMaximumInvocationDepthIsZero_FailsOptionsValidation()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentHooks(static options => options.MaximumInvocationDepth = 0);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<AgentHookOptions>>().Value);
        _ = Should.Throw<OptionsValidationException>(provider.GetRequiredService<IHookDispatcher>);
    }

    [Fact]
    public void AddAgentHooks_WhenMinimumFailureModeUndefined_FailsOptionsValidation()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentHooks(static options => options.MinimumFailureMode = (HookFailureMode) 42);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<AgentHookOptions>>().Value);
        _ = Should.Throw<OptionsValidationException>(provider.GetRequiredService<IHookDispatcher>);
    }

    [Fact]
    public async Task AddAgentHooks_WhenDepthCeilingConfigured_ResolvedDispatcherHonorsCeiling()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentHooks(static options => options.MaximumInvocationDepth = 1);
        using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IHookDispatcher>();
        var innerHooks = new[] { new TestHook { Id = new HookId("inner") } };
        var outerHooks = new[]
        {
            new TestHook
            {
                Id = new HookId("outer"),
                OnInvoke = async (args, scope, ct) => await dispatcher.DispatchAsync(_point, innerHooks, args, Invoker, scope, maxReentrantDepth: 3, cancellationToken: ct)
            }
        };

        // The caller asks for depth 3 on both levels; the host ceiling of 1 rejects the nested dispatch.
        _ = await Should.ThrowAsync<HookReentrancyException>(() => dispatcher.DispatchAsync(_point, outerHooks, new TestHookEventArgs(), Invoker, HookDispatchScope.Root, maxReentrantDepth: 3, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddAgentHooks_WhenMinimumFailureModeConfigured_ResolvedDispatcherEscalatesIsolation()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentHooks(static options => options.MinimumFailureMode = HookFailureMode.FailOperation);
        using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IHookDispatcher>();
        var hooks = new[]
        {
            new TestHook { Id = new HookId("a"), OnInvoke = static (_, _, _) => throw new InvalidOperationException("boom") },
            new TestHook { Id = new HookId("b") }
        };
        var args = new TestHookEventArgs();

        _ = await Should.ThrowAsync<InvalidOperationException>(() => dispatcher.DispatchAsync(_point, hooks, args, Invoker, HookDispatchScope.Root, HookFailureMode.Isolate, cancellationToken: TestContext.Current.CancellationToken));

        args.InvocationOrder.ShouldBe([new HookId("a")]);
    }

    private static Func<TestHook, TestHookEventArgs, HookDispatchScope, CancellationToken, Task> Invoker => static (hook, args, scope, ct) => hook.InvokeAsync(args, scope, ct);

    private sealed class TestRunStartedHook: IRunStartedHook
    {
        public HookId Id { get; } = new("test.run-started");

        public ValueTask OnRunStartedAsync(RunStartedEventArgs args, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class TestBeforeModelRequestHook: IBeforeModelRequestHook
    {
        public HookId Id { get; } = new("test.before-model");

        public ValueTask OnBeforeModelRequestAsync(BeforeModelRequestEventArgs args, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class OtherBeforeModelRequestHook: IBeforeModelRequestHook
    {
        public HookId Id { get; } = new("test.before-model.other");

        public ValueTask OnBeforeModelRequestAsync(BeforeModelRequestEventArgs args, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class TestBeforeToolInvocationHook: IBeforeToolInvocationHook
    {
        public HookId Id { get; } = new("test.before-tool");

        public ValueTask OnBeforeToolInvocationAsync(BeforeToolInvocationEventArgs args, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
