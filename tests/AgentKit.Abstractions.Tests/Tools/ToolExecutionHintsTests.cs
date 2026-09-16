// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

public sealed class ToolExecutionHintsTests
{
    [Fact]
    public void Constructor_WhenModeUndefined_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => new ToolExecutionHints((ToolSchedulingMode) 99, null, null, null))
            .ParamName.ShouldBe("schedulingMode");
    }

    [Fact]
    public void Constructor_WhenConcurrencyKeyInconsistent_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => new ToolExecutionHints(ToolSchedulingMode.ConcurrencyKey, null, null, null))
            .ParamName.ShouldBe("concurrencyKey");
        Should.Throw<ArgumentException>(() => new ToolExecutionHints(ToolSchedulingMode.Sequential, "key", null, null))
            .ParamName.ShouldBe("concurrencyKey");
        Should.Throw<ArgumentException>(() => new ToolExecutionHints(ToolSchedulingMode.ConcurrencyKey, " ", null, null))
            .ParamName.ShouldBe("concurrencyKey");
    }

    [Fact]
    public void Constructor_WhenExpectedDurationNegative_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => new ToolExecutionHints(ToolSchedulingMode.Unspecified, null, TimeSpan.FromTicks(-1), null))
            .ParamName.ShouldBe("expectedDuration");
    }

    [Fact]
    public void Constructor_WhenValuesValid_PreservesOptionalEvidence()
    {
        var hints = new ToolExecutionHints(ToolSchedulingMode.ConcurrencyKey, "workspace", TimeSpan.Zero, false);

        hints.SchedulingMode.ShouldBe(ToolSchedulingMode.ConcurrencyKey);
        hints.ConcurrencyKey.ShouldBe("workspace");
        hints.ExpectedDuration.ShouldBe(TimeSpan.Zero);
        hints.ApprovalMayBeCached.ShouldBe(false);
    }

    [Theory]
    [InlineData(ToolSchedulingMode.Unspecified)]
    [InlineData(ToolSchedulingMode.ParallelSafe)]
    [InlineData(ToolSchedulingMode.Sequential)]
    [InlineData(ToolSchedulingMode.GlobalExclusive)]
    [InlineData(ToolSchedulingMode.HostScheduled)]
    public void Constructor_WhenModeNeedsNoKey_AcceptsUnassertedOptions(ToolSchedulingMode mode)
    {
        var hints = new ToolExecutionHints(mode, null, null, null);

        hints.SchedulingMode.ShouldBe(mode);
        hints.ExpectedDuration.ShouldBeNull();
        hints.ApprovalMayBeCached.ShouldBeNull();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ToolExecutionHints(ToolSchedulingMode.Unspecified, null, null, null);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
