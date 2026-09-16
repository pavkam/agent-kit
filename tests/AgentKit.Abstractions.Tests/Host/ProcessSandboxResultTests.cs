// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

/// <summary>Verifies ProcessSandboxResult behavior and contracts.</summary>
public sealed class ProcessSandboxResultTests
{
    [Fact]
    public void Constructor_WhenStatusIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ProcessSandboxResult((ProcessSandboxStatus) 99, null, "message")).ParamName.ShouldBe("status");

    [Fact]
    public void Constructor_WhenReadyStatusHasNoLaunch_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ProcessSandboxResult(ProcessSandboxStatus.Ready, null, null)).ParamName.ShouldBe("launch");

    [Fact]
    public void Constructor_WhenNonReadyStatusHasLaunch_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ProcessSandboxResult(ProcessSandboxStatus.Unavailable, Launch(), "message")).ParamName.ShouldBe("launch");

    [Fact]
    public void Constructor_WhenNonReadyStatusHasNoSafeMessage_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ProcessSandboxResult(ProcessSandboxStatus.Unavailable, null, null)).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void Constructor_WhenReadyResultIsValid_RoundTripsProperties()
    {
        var launch = Launch();
        var result = new ProcessSandboxResult(ProcessSandboxStatus.Ready, launch, null);
        result.Status.ShouldBe(ProcessSandboxStatus.Ready);
        result.Launch.ShouldBeSameAs(launch);
        result.SafeMessage.ShouldBeNull();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ProcessSandboxResult(ProcessSandboxStatus.Ready, Launch(), null);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ProcessSandboxLaunch Launch() => new("/usr/bin/sandbox", ["--profile"]);
}
