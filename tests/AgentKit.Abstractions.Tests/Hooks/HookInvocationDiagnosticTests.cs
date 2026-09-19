// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit;

public sealed class HookInvocationDiagnosticTests
{
    private static HookInvocationContext Invocation { get; } = new(
        new HookRegistrationId(Guid.NewGuid()), new HookInvocationId(Guid.NewGuid()), new HookDispatchId(Guid.NewGuid()), 1);

    [Fact]
    public void Constructor_WhenPointIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookInvocationDiagnostic(
            default, Invocation, HookInvocationOutcome.Succeeded, DateTimeOffset.UnixEpoch, TimeSpan.Zero));

        exception.ParamName.ShouldBe("point");
    }

    [Fact]
    public void Constructor_WhenInvocationIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new HookInvocationDiagnostic(
            HookKernelTestData.Point, null!, HookInvocationOutcome.Succeeded, DateTimeOffset.UnixEpoch, TimeSpan.Zero));

        exception.ParamName.ShouldBe("invocation");
    }

    [Fact]
    public void Constructor_WhenOutcomeIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookInvocationDiagnostic(
            HookKernelTestData.Point, Invocation, (HookInvocationOutcome) 99, DateTimeOffset.UnixEpoch, TimeSpan.Zero));

        exception.ParamName.ShouldBe("outcome");
    }

    [Fact]
    public void Constructor_WhenDurationIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookInvocationDiagnostic(
            HookKernelTestData.Point, Invocation, HookInvocationOutcome.Succeeded, DateTimeOffset.UnixEpoch, TimeSpan.FromSeconds(-1)));

        exception.ParamName.ShouldBe("duration");
    }

    [Fact]
    public void Constructor_WhenValid_RoundTripsEveryProperty()
    {
        var timestamp = DateTimeOffset.UnixEpoch;
        var duration = TimeSpan.FromMilliseconds(5);

        var diagnostic = new HookInvocationDiagnostic(
            HookKernelTestData.Point, Invocation, HookInvocationOutcome.Failed, timestamp, duration);

        diagnostic.Point.ShouldBe(HookKernelTestData.Point);
        diagnostic.Invocation.ShouldBe(Invocation);
        diagnostic.Outcome.ShouldBe(HookInvocationOutcome.Failed);
        diagnostic.StartedAt.ShouldBe(timestamp);
        diagnostic.Duration.ShouldBe(duration);
    }
}
