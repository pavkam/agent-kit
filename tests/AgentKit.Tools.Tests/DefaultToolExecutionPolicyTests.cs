// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using Microsoft.Extensions.Options;

using static ToolRuntimeTestCalls;

/// <summary>Verifies <see cref="DefaultToolExecutionPolicy"/> planning from <see cref="ToolRuntimeOptions"/>.</summary>
public sealed class DefaultToolExecutionPolicyTests
{
    [Fact]
    public void StandardReference_WhenRead_NamesTheStandardFamilyAtRevisionOne()
    {
        DefaultToolExecutionPolicy.StandardReference.Key.Value.ShouldBe("standard");
        DefaultToolExecutionPolicy.StandardReference.Version.ShouldBe(new ToolExecutionPolicyVersion(1));
    }

    [Theory]
    [InlineData(30, 9, 30)]
    [InlineData(5, 9, 9)]
    [InlineData(7_200, 9, 3_600)]
    public async Task PlanAsync_WhenADescriptorExpectsADuration_GrantsItOnlyAboveTheConfiguredTimeoutAndUpToTheMaximum(int expectedSeconds, int timeoutSeconds, int plannedSeconds)
    {
        var options = new ToolRuntimeOptions { InvocationTimeout = TimeSpan.FromSeconds(timeoutSeconds), MaximumInvocationTimeout = TimeSpan.FromHours(1) };
        var policy = new DefaultToolExecutionPolicy(Standard, Options.Create(options));
        var call = Validated(hints: new ToolExecutionHints(ToolSchedulingMode.Sequential, null, TimeSpan.FromSeconds(expectedSeconds), null));

        var result = await policy.PlanAsync([call], Context(), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ToolExecutionPlanned>().Calls[0].ExecutionPlan.InvocationTimeout.ShouldBe(TimeSpan.FromSeconds(plannedSeconds));
    }

    [Fact]
    public async Task PlanAsync_WhenCallsNameTheReference_PlansEachCallFromTheConfiguredOptions()
    {
        var options = new ToolRuntimeOptions
        {
            MaximumAttempts = 4,
            RetryInitialDelay = TimeSpan.FromMilliseconds(50),
            RetryBackoffMultiplier = 3.0,
            RetryMaximumDelay = TimeSpan.FromSeconds(2),
            RetryJitterFraction = 0.1,
            InvocationTimeout = TimeSpan.FromSeconds(9),
        };
        var policy = new DefaultToolExecutionPolicy(Standard, Options.Create(options));
        var first = Validated(suffix: 1);
        var second = Validated(hints: new ToolExecutionHints(ToolSchedulingMode.Sequential, null, null, null), suffix: 2);

        var result = await policy.PlanAsync([first, second], Context(), TestContext.Current.CancellationToken);

        var planned = result.ShouldBeOfType<ToolExecutionPlanned>();
        planned.Calls.Select(static call => call.Call).ShouldBe([first, second]);
        var plan = planned.Calls[0].ExecutionPlan;
        plan.Retry.MaximumAttempts.ShouldBe(4);
        plan.Retry.InitialDelay.ShouldBe(TimeSpan.FromMilliseconds(50));
        plan.Retry.BackoffMultiplier.ShouldBe(3.0);
        plan.Retry.MaximumDelay.ShouldBe(TimeSpan.FromSeconds(2));
        plan.Retry.JitterFraction.ShouldBe(0.1);
        plan.InvocationTimeout.ShouldBe(TimeSpan.FromSeconds(9));
        plan.Normalization.ExecutionPolicy.ShouldBe(Standard);
        planned.Calls[0].ExecutionPlan.Scheduling.ShouldBe(first.Tool.ExecutionHints);
        planned.Calls[1].ExecutionPlan.Scheduling.SchedulingMode.ShouldBe(ToolSchedulingMode.Sequential);
    }

    [Fact]
    public async Task PlanAsync_WhenCallNamesAnotherReference_ThrowsExactParameter()
    {
        var policy = new DefaultToolExecutionPolicy(Standard, Options.Create(new ToolRuntimeOptions()));
        var other = Validated(policy: new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("other"), new ToolExecutionPolicyVersion(1)));

        var exception = await Should.ThrowAsync<ArgumentException>(async () => await policy.PlanAsync([other], Context(), TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("calls");
    }

    [Fact]
    public async Task PlanAsync_WhenArgumentsAreInvalid_ThrowsExactParameter()
    {
        var policy = new DefaultToolExecutionPolicy(Standard, Options.Create(new ToolRuntimeOptions()));

        (await Should.ThrowAsync<ArgumentException>(async () => await policy.PlanAsync(default, Context()))).ParamName.ShouldBe("calls");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await policy.PlanAsync([Validated()], null!))).ParamName.ShouldBe("context");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await policy.PlanAsync([null!], Context()))).ParamName.ShouldBe("calls");
    }

    [Fact]
    public async Task PlanAsync_WhenCancelled_ThrowsCancellation()
    {
        var policy = new DefaultToolExecutionPolicy(Standard, Options.Create(new ToolRuntimeOptions()));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await policy.PlanAsync([Validated()], Context(), cancellation.Token));
    }

    [Fact]
    public void Constructor_WhenDependencyIsNull_ThrowsExactParameter()
    {
        Should.Throw<ArgumentNullException>(() => new DefaultToolExecutionPolicy(null!, Options.Create(new ToolRuntimeOptions()))).ParamName.ShouldBe("reference");
        Should.Throw<ArgumentNullException>(() => new DefaultToolExecutionPolicy(Standard, null!)).ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenRetryPacingIsImpossible_ThrowsAtComposition() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new DefaultToolExecutionPolicy(Standard, Options.Create(new ToolRuntimeOptions { MaximumAttempts = 0 })));
}
