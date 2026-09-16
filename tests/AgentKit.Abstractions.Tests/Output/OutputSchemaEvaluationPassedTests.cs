// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

/// <summary>Verifies OutputSchemaEvaluationPassed behavior and contracts.</summary>
public sealed class OutputSchemaEvaluationPassedTests
{
    [Fact]
    public void Instance_WhenAccessed_ReturnsSharedInstance() =>
        OutputSchemaEvaluationPassed.Instance.ShouldBeSameAs(OutputSchemaEvaluationPassed.Instance);

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = OutputSchemaEvaluationPassed.Instance;
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
