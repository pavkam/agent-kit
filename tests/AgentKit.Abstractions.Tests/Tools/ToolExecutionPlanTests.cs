// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

using static ToolRuntimeTestFixture;

/// <summary>Verifies <see cref="ToolExecutionPlan"/> validation.</summary>
public sealed class ToolExecutionPlanTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var hints = ExecutionHints();
        var snapshot = NormalizationSnapshot();
        var plan = new ToolExecutionPlan(hints, ToolRetryPolicy.NoRetry, TimeSpan.FromSeconds(3), snapshot);

        plan.Scheduling.ShouldBeSameAs(hints);
        plan.Retry.ShouldBeSameAs(ToolRetryPolicy.NoRetry);
        plan.InvocationTimeout.ShouldBe(TimeSpan.FromSeconds(3));
        plan.Normalization.ShouldBeSameAs(snapshot);
    }

    [Fact]
    public void Constructor_WhenSchedulingIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolExecutionPlan(null!, ToolRetryPolicy.NoRetry, TimeSpan.FromSeconds(1), NormalizationSnapshot()))
            .ParamName.ShouldBe("scheduling");

    [Fact]
    public void Constructor_WhenRetryIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolExecutionPlan(ExecutionHints(), null!, TimeSpan.FromSeconds(1), NormalizationSnapshot()))
            .ParamName.ShouldBe("retry");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenTimeoutIsNotPositive_ThrowsExactParameter(int seconds) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolExecutionPlan(ExecutionHints(), ToolRetryPolicy.NoRetry, TimeSpan.FromSeconds(seconds), NormalizationSnapshot()))
            .ParamName.ShouldBe("invocationTimeout");

    [Fact]
    public void Constructor_WhenNormalizationIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolExecutionPlan(ExecutionHints(), ToolRetryPolicy.NoRetry, TimeSpan.FromSeconds(1), null!))
            .ParamName.ShouldBe("normalization");

    [Fact]
    public void Constructor_WhenNormalizationNamesNoExecutionPolicy_ThrowsExactParameter()
    {
        var rejection = new ToolResultNormalizationSnapshot(
            NormalizationSnapshot().RejectionPolicy,
            NormalizationSnapshot().ProjectionPolicy,
            executionPolicy: null,
            new ToolResultNormalizationAlgorithmVersion(1),
            new ToolResultBounds(1024, 4),
            ToolResultProjectionTransformations.None,
            ExtensionData.Empty);

        Should.Throw<ArgumentNullException>(() => new ToolExecutionPlan(ExecutionHints(), ToolRetryPolicy.NoRetry, TimeSpan.FromSeconds(1), rejection))
            .ParamName.ShouldBe("normalization");
    }
}
