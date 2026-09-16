// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

/// <summary>Verifies RunPolicyDefaults behavior and contracts.</summary>
public sealed class RunPolicyDefaultsTests
{
    [Fact]
    public void Constructor_WhenMaxTurnsIsNotPositive_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RunPolicyDefaults(0, TimeSpan.FromMinutes(1))).ParamName.ShouldBe("maxTurns");

    [Fact]
    public void Constructor_WhenAttemptTimeoutIsNotPositive_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RunPolicyDefaults(1, TimeSpan.Zero)).ParamName.ShouldBe("attemptTimeout");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var defaults = new RunPolicyDefaults(8, TimeSpan.FromMinutes(1));
        defaults.MaxTurns.ShouldBe(8);
        defaults.AttemptTimeout.ShouldBe(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Default_WhenAccessed_HasConservativeValues()
    {
        RunPolicyDefaults.Default.MaxTurns.ShouldBe(8);
        RunPolicyDefaults.Default.AttemptTimeout.ShouldBe(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void With_WhenMaxTurnsIsNotPositive_ThrowsExactParameter()
    {
        var defaults = new RunPolicyDefaults(8, TimeSpan.FromMinutes(1));
        Should.Throw<ArgumentOutOfRangeException>(() => _ = defaults with { MaxTurns = 0 }).ParamName.ShouldBe("MaxTurns");
    }

    [Fact]
    public void With_WhenAttemptTimeoutIsNotPositive_ThrowsExactParameter()
    {
        var defaults = new RunPolicyDefaults(8, TimeSpan.FromMinutes(1));
        Should.Throw<ArgumentOutOfRangeException>(() => _ = defaults with { AttemptTimeout = TimeSpan.Zero }).ParamName.ShouldBe("AttemptTimeout");
    }

    [Fact]
    public void With_WhenValuesAreValid_UpdatesProperties()
    {
        var defaults = new RunPolicyDefaults(8, TimeSpan.FromMinutes(1));
        var changed = defaults with { MaxTurns = 4, AttemptTimeout = TimeSpan.FromMinutes(2) };
        changed.MaxTurns.ShouldBe(4);
        changed.AttemptTimeout.ShouldBe(TimeSpan.FromMinutes(2));
    }
}
