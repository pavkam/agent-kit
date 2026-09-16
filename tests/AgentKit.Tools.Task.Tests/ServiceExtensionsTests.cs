// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Task.Tests;



/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddTaskTool_WhenCalledTwice_RegistersOneToolAndOneIdentitySource()
    {
        var services = new ServiceCollection();
        _ = services.AddTaskTool().AddTaskTool();
        services.Count(descriptor => descriptor.ServiceType == typeof(ITool) && descriptor.ImplementationType == typeof(TaskTool)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IIdentifierGenerator<DelegationId>)).ShouldBe(1);
    }

    [Fact]
    public void AddTaskTool_WhenConfigureProvided_AppliesConfiguredBounds()
    {
        var services = new ServiceCollection();
        _ = services.AddTaskTool(static options => options.MaximumAllowedTools = 3);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<TaskToolOptions>>().Value.MaximumAllowedTools.ShouldBe(3);
    }

    [Fact]
    public void AddTaskTool_WhenDefaultOptions_PassesValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddTaskTool();
        using var provider = services.BuildServiceProvider();

        _ = Should.NotThrow(() => provider.GetRequiredService<IOptions<TaskToolOptions>>().Value);
    }

    [Theory]
    [InlineData(0, 60, 5, 10, 5, 10, 1000, 5, 500, 20, 2000)]
    [InlineData(30, 10, 5, 10, 5, 10, 1000, 5, 500, 20, 2000)]
    [InlineData(30, 60, 0, 10, 5, 10, 1000, 5, 500, 20, 2000)]
    [InlineData(30, 60, 5, 4, 5, 10, 1000, 5, 500, 20, 2000)]
    [InlineData(30, 60, 5, 10, 0, 10, 1000, 5, 500, 20, 2000)]
    [InlineData(30, 60, 5, 10, 5, 4, 1000, 5, 500, 20, 2000)]
    [InlineData(30, 60, 5, 10, 5, 10, 0, 5, 500, 20, 2000)]
    [InlineData(30, 60, 5, 10, 5, 10, 1000, 0, 500, 20, 2000)]
    [InlineData(30, 60, 5, 10, 5, 10, 1000, 5, 0, 20, 2000)]
    [InlineData(30, 60, 5, 10, 5, 10, 1000, 5, 500, 0, 2000)]
    [InlineData(30, 60, 5, 10, 5, 10, 1000, 5, 500, 20, 0)]
    public void AddTaskTool_WhenAnyBoundIsInvalid_ThrowsOptionsValidationException(
        int defaultTimeoutSeconds,
        int maximumTimeoutSeconds,
        int defaultMaximumTurns,
        int maximumTurns,
        int defaultMaximumToolCalls,
        int maximumToolCalls,
        int maximumObjectiveCharacters,
        int maximumAcceptanceCriteria,
        int maximumCriterionCharacters,
        int maximumAllowedTools,
        int maximumSummaryCharacters)
    {
        var services = new ServiceCollection();
        _ = services.AddTaskTool(options =>
        {
            options.DefaultTimeout = TimeSpan.FromSeconds(defaultTimeoutSeconds);
            options.MaximumTimeout = TimeSpan.FromSeconds(maximumTimeoutSeconds);
            options.DefaultMaximumTurns = defaultMaximumTurns;
            options.MaximumTurns = maximumTurns;
            options.DefaultMaximumToolCalls = defaultMaximumToolCalls;
            options.MaximumToolCalls = maximumToolCalls;
            options.MaximumObjectiveCharacters = maximumObjectiveCharacters;
            options.MaximumAcceptanceCriteria = maximumAcceptanceCriteria;
            options.MaximumCriterionCharacters = maximumCriterionCharacters;
            options.MaximumAllowedTools = maximumAllowedTools;
            options.MaximumSummaryCharacters = maximumSummaryCharacters;
        });
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<TaskToolOptions>>().Value);
    }
}
