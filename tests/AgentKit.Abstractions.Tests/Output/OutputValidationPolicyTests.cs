// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

using AgentKit;

/// <summary>Verifies construction and copy invariants for <see cref="OutputValidationPolicy"/>.</summary>
public sealed class OutputValidationPolicyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaximumIssuesIsNotPositive_ThrowsArgumentOutOfRangeException(int maximumIssues)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new OutputValidationPolicy(OutputValidationFailureMode.CollectAllFailures, maximumIssues));

        exception.ParamName.ShouldBe("maximumIssues");
    }

    [Fact]
    public void Constructor_WhenFailureModeIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new OutputValidationPolicy((OutputValidationFailureMode) int.MaxValue, 1));

        exception.ParamName.ShouldBe("failureMode");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void With_WhenMaximumIssuesIsNotPositive_ThrowsArgumentOutOfRangeException(int maximumIssues)
    {
        var policy = new OutputValidationPolicy(OutputValidationFailureMode.CollectAllFailures, 2);

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => policy with { MaximumIssues = maximumIssues });

        exception.ParamName.ShouldBe("MaximumIssues");
        policy.MaximumIssues.ShouldBe(2);
    }

    [Fact]
    public void With_WhenFailureModeIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var policy = new OutputValidationPolicy(OutputValidationFailureMode.CollectAllFailures, 2);

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => policy with { FailureMode = (OutputValidationFailureMode) int.MaxValue });

        exception.ParamName.ShouldBe("FailureMode");
        policy.FailureMode.ShouldBe(OutputValidationFailureMode.CollectAllFailures);
    }

    [Fact]
    public void With_WhenValuesAreValid_CreatesChangedCopyAndLeavesOriginalUnchanged()
    {
        var policy = new OutputValidationPolicy(OutputValidationFailureMode.RejectOnFirstFailure, 1);

        var changed = policy with
        {
            FailureMode = OutputValidationFailureMode.CollectAllFailures,
            MaximumIssues = 3,
        };

        changed.FailureMode.ShouldBe(OutputValidationFailureMode.CollectAllFailures);
        changed.MaximumIssues.ShouldBe(3);
        policy.FailureMode.ShouldBe(OutputValidationFailureMode.RejectOnFirstFailure);
        policy.MaximumIssues.ShouldBe(1);
    }
}
