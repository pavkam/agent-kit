// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

/// <summary>Verifies OutputValidationPassed behavior and contracts.</summary>
public sealed class OutputValidationPassedTests
{
    [Fact]
    public void Instance_WhenAccessed_ReturnsSharedInstance() =>
        OutputValidationPassed.Instance.ShouldBeSameAs(OutputValidationPassed.Instance);

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = OutputValidationPassed.Instance;
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
