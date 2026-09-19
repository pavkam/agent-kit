// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

/// <summary>Verifies AgentComponentSelection behavior and contracts.</summary>
public sealed class AgentComponentSelectionTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var selection = Selection();
        selection.Loop.ShouldBe(new ComponentKey<IAgentLoop>("loop"));
        selection.ContinuationPolicy.ShouldBe(new ComponentKey<IRunContinuationPolicy>("continuation"));
        selection.Input.ShouldBe(new ComponentKey<IInputCoordinator>("input"));
        selection.Output.ShouldBe(new ComponentKey<IOutputPublisher>("output"));
        selection.OutputProcessor.ShouldBe(new ComponentKey<IOutputProcessor>("output-processor"));
        selection.Context.ShouldBe(new ComponentKey<IContextAssembler>("context"));
        selection.ModelSelector.ShouldBe(new ComponentKey<IModelSelector>("model-selector"));
        selection.ModelExecutor.ShouldBe(new ComponentKey<IModelRequestExecutor>("model-executor"));
        selection.BudgetProfile.ShouldBe(new BudgetProfileKey("budget"));
    }

    [Theory]
    [InlineData("loop")]
    [InlineData("continuationPolicy")]
    [InlineData("input")]
    [InlineData("output")]
    [InlineData("outputProcessor")]
    [InlineData("context")]
    [InlineData("modelSelector")]
    [InlineData("modelExecutor")]
    [InlineData("budgetProfile")]
    public void Constructor_WhenAnySelectionIsDefault_ThrowsExactArgumentOutOfRangeException(string parameter)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new AgentComponentSelection(
            parameter == "loop" ? default : new ComponentKey<IAgentLoop>("loop"),
            parameter == "continuationPolicy" ? default : new ComponentKey<IRunContinuationPolicy>("continuation"),
            parameter == "input" ? default : new ComponentKey<IInputCoordinator>("input"),
            parameter == "output" ? default : new ComponentKey<IOutputPublisher>("output"),
            parameter == "outputProcessor" ? default : new ComponentKey<IOutputProcessor>("output-processor"),
            parameter == "context" ? default : new ComponentKey<IContextAssembler>("context"),
            parameter == "modelSelector" ? default : new ComponentKey<IModelSelector>("model-selector"),
            parameter == "modelExecutor" ? default : new ComponentKey<IModelRequestExecutor>("model-executor"),
            parameter == "budgetProfile" ? default : new BudgetProfileKey("budget")));

        exception.ParamName.ShouldBe(parameter);
    }

    [Fact]
    public void Equality_WhenEverySelectionMatches_IsEqual()
    {
        Selection().ShouldBe(Selection());
        Selection().GetHashCode().ShouldBe(Selection().GetHashCode());
    }

    [Fact]
    public void Equality_WhenOneSelectionDiffers_IsNotEqual() =>
        Selection().ShouldNotBe(new AgentComponentSelection(
            new ComponentKey<IAgentLoop>("other"),
            new ComponentKey<IRunContinuationPolicy>("continuation"),
            new ComponentKey<IInputCoordinator>("input"),
            new ComponentKey<IOutputPublisher>("output"),
            new ComponentKey<IOutputProcessor>("output-processor"),
            new ComponentKey<IContextAssembler>("context"),
            new ComponentKey<IModelSelector>("model-selector"),
            new ComponentKey<IModelRequestExecutor>("model-executor"),
            new BudgetProfileKey("budget")));

    private static AgentComponentSelection Selection() => new(
        new ComponentKey<IAgentLoop>("loop"),
        new ComponentKey<IRunContinuationPolicy>("continuation"),
        new ComponentKey<IInputCoordinator>("input"),
        new ComponentKey<IOutputPublisher>("output"),
        new ComponentKey<IOutputProcessor>("output-processor"),
        new ComponentKey<IContextAssembler>("context"),
        new ComponentKey<IModelSelector>("model-selector"),
        new ComponentKey<IModelRequestExecutor>("model-executor"),
        new BudgetProfileKey("budget"));
}
