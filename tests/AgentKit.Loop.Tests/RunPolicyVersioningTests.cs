// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

/// <summary>Verifies RunPolicyVersioning behavior and contracts.</summary>
public sealed class RunPolicyVersioningTests
{
    [Fact]
    public void Compute_WhenMaxTurnsIsNotPositive_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => RunPolicyVersioning.Compute(0, TimeSpan.FromMinutes(1), Key(), Options()));
        exception.ParamName.ShouldBe("maxTurns");
    }

    [Fact]
    public void Compute_WhenAttemptTimeoutIsNotPositive_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => RunPolicyVersioning.Compute(8, TimeSpan.Zero, Key(), Options()));
        exception.ParamName.ShouldBe("attemptTimeout");
    }

    [Fact]
    public void Compute_WhenContinuationPolicyKeyIsBlank_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(1), default, Options()));
        exception.ParamName.ShouldBe("continuationPolicyKey");
    }

    [Fact]
    public void Compute_WhenOptionsIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(1), Key(), null!));
        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Compute_WhenCalledTwiceWithIdenticalArguments_ReturnsTheSameVersion()
    {
        var first = RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key(), Options());
        var second = RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key(), Options());

        first.ShouldBe(second);
    }

    [Fact]
    public void Compute_WhenMaxTurnsDiffers_ReturnsADifferentVersion()
    {
        var first = RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key(), Options());
        var second = RunPolicyVersioning.Compute(9, TimeSpan.FromMinutes(2), Key(), Options());

        first.ShouldNotBe(second);
    }

    [Fact]
    public void Compute_WhenAttemptTimeoutDiffers_ReturnsADifferentVersion()
    {
        var first = RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key(), Options());
        var second = RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(3), Key(), Options());

        first.ShouldNotBe(second);
    }

    [Fact]
    public void Compute_WhenContinuationPolicyKeyDiffers_ReturnsADifferentVersion()
    {
        var first = RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key("a"), Options());
        var second = RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key("b"), Options());

        first.ShouldNotBe(second);
    }

    [Fact]
    public void Compute_WhenAnyLoopOptionDiffers_ReturnsADifferentVersion()
    {
        var baseline = Options();
        var first = RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key(), baseline);

        RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key(),
            new AgentLoopOptions { HistoryReadPageSize = baseline.HistoryReadPageSize + 1 }).ShouldNotBe(first);
        RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key(),
            new AgentLoopOptions { AppendConflictRetryLimit = baseline.AppendConflictRetryLimit + 1 }).ShouldNotBe(first);
        RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key(),
            new AgentLoopOptions { DisableToolsOnFinalTurn = !baseline.DisableToolsOnFinalTurn }).ShouldNotBe(first);
        RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key(),
            new AgentLoopOptions { SettlementTimeout = baseline.SettlementTimeout + TimeSpan.FromSeconds(1) }).ShouldNotBe(first);
        RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key(),
            new AgentLoopOptions { ObserverDeliveryTimeout = baseline.ObserverDeliveryTimeout + TimeSpan.FromSeconds(1) }).ShouldNotBe(first);
        RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key(),
            new AgentLoopOptions { ContextPressureThreshold = baseline.ContextPressureThreshold / 2 }).ShouldNotBe(first);
        RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key(),
            new AgentLoopOptions { EstimatedCharactersPerToken = baseline.EstimatedCharactersPerToken + 1 }).ShouldNotBe(first);
    }

    [Fact]
    public void Compute_Always_ReturnsAPositiveVersion() =>
        RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key(), Options()).Value.ShouldBeGreaterThan(0);

    private static ComponentKey<IRunContinuationPolicy> Key(string value = "continuation") => new(value);

    private static AgentLoopOptions Options() => new();
}
