// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks.Tests;

/// <summary>Verifies the documented <see cref="AgentHookOptions"/> defaults.</summary>
public sealed class AgentHookOptionsTests
{
    [Fact]
    public void MaximumInvocationDepth_WhenDefault_IsEight()
    {
        var options = new AgentHookOptions();

        options.MaximumInvocationDepth.ShouldBe(8);
    }

    [Fact]
    public void MinimumFailureMode_WhenDefault_IsLeastStrictMode()
    {
        // IsolateAndDiagnose < FailOperation in strictness, so the default must be IsolateAndDiagnose to leave caller modes unchanged.
        var options = new AgentHookOptions();

        options.MinimumFailureMode.ShouldBe(HookFailureMode.IsolateAndDiagnose);
    }

    [Fact]
    public void DefaultHookTimeout_WhenDefault_IsTenSeconds() =>
        new AgentHookOptions().DefaultHookTimeout.ShouldBe(TimeSpan.FromSeconds(10));

    [Fact]
    public void MutationDispatchMode_WhenDefault_IsSequential() =>
        new AgentHookOptions().MutationDispatchMode.ShouldBe(HookMutationDispatchMode.Sequential);

    [Fact]
    public void ReloadBoundary_WhenDefault_IsNextRun() =>
        new AgentHookOptions().ReloadBoundary.ShouldBe(HookReloadBoundary.NextRun);

    [Fact]
    public void AddAgentHooks_WhenMutationDispatchModeConcurrent_FailsOptionsValidation()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentHooks(static options => options.MutationDispatchMode = HookMutationDispatchMode.Concurrent);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<AgentHookOptions>>().Value);
    }

    [Fact]
    public void AddAgentHooks_WhenDefaultHookTimeoutZero_FailsOptionsValidation()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentHooks(static options => options.DefaultHookTimeout = TimeSpan.Zero);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<AgentHookOptions>>().Value);
    }
}
