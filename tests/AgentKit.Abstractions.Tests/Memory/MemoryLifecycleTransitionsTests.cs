// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

/// <summary>Verifies the <see cref="MemoryLifecycleTransitions"/> table.</summary>
public sealed class MemoryLifecycleTransitionsTests
{
    [Theory]
    [InlineData(MemoryLifecycleState.Proposed, MemoryLifecycleState.Validated)]
    [InlineData(MemoryLifecycleState.Proposed, MemoryLifecycleState.Rejected)]
    [InlineData(MemoryLifecycleState.Validated, MemoryLifecycleState.Accepted)]
    [InlineData(MemoryLifecycleState.Validated, MemoryLifecycleState.Rejected)]
    [InlineData(MemoryLifecycleState.Accepted, MemoryLifecycleState.Active)]
    [InlineData(MemoryLifecycleState.Accepted, MemoryLifecycleState.Rejected)]
    [InlineData(MemoryLifecycleState.Active, MemoryLifecycleState.Corrected)]
    [InlineData(MemoryLifecycleState.Active, MemoryLifecycleState.Expired)]
    public void IsAllowed_WhenTransitionIsInTheTable_ReturnsTrue(MemoryLifecycleState from, MemoryLifecycleState to) =>
        MemoryLifecycleTransitions.IsAllowed(from, to).ShouldBeTrue();

    [Fact]
    public void IsAllowed_WhenCheckedAcrossEveryPair_AllowsExactlyTheTableAndNeverDeletion()
    {
        var states = Enum.GetValues<MemoryLifecycleState>();

        var allowed = (from source in states from target in states where MemoryLifecycleTransitions.IsAllowed(source, target) select (source, target)).ToArray();

        allowed.Length.ShouldBe(8);
        allowed.ShouldNotContain(pair => pair.target == MemoryLifecycleState.Deleted);
        allowed.Select(static pair => pair.source).Distinct().Order().ShouldBe(
            [MemoryLifecycleState.Proposed, MemoryLifecycleState.Validated, MemoryLifecycleState.Accepted, MemoryLifecycleState.Active]);
    }

    [Theory]
    [InlineData(MemoryLifecycleState.Proposed, true)]
    [InlineData(MemoryLifecycleState.Validated, true)]
    [InlineData(MemoryLifecycleState.Accepted, true)]
    [InlineData(MemoryLifecycleState.Active, true)]
    [InlineData(MemoryLifecycleState.Rejected, false)]
    [InlineData(MemoryLifecycleState.Corrected, false)]
    [InlineData(MemoryLifecycleState.Deleted, false)]
    [InlineData(MemoryLifecycleState.Expired, false)]
    public void IsCreatable_WhenStateIsChecked_AllowsOnlyNonTerminalStates(MemoryLifecycleState state, bool expected) =>
        MemoryLifecycleTransitions.IsCreatable(state).ShouldBe(expected);
}
