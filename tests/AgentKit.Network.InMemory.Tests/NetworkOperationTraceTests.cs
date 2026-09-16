// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory.Tests;

/// <summary>Verifies NetworkOperationTrace behavior and contracts.</summary>
public sealed class NetworkOperationTraceTests
{
    [Fact]
    public void Constructor_WhenDestinationIsNull_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new NetworkOperationTrace(Id(), null!, "send", DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("destination");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenPhaseIsBlank_ThrowsWithExactParameterName(string? phase)
    {
        var exception = Should.Throw<ArgumentException>(() => new NetworkOperationTrace(Id(), Destination(), phase!, DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("phase");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesExactValues()
    {
        var id = Id();
        var destination = Destination();
        var at = DateTimeOffset.UnixEpoch.AddMinutes(5);
        var trace = new NetworkOperationTrace(id, destination, "resolve", at);
        trace.Id.ShouldBe(id);
        trace.Destination.ShouldBe(destination);
        trace.Phase.ShouldBe("resolve");
        trace.At.ShouldBe(at);
    }

    [Fact]
    public void With_WhenNoMembersChanged_ProducesAnEqualClone()
    {
        var trace = new NetworkOperationTrace(Id(), Destination(), "send", DateTimeOffset.UnixEpoch);
        var clone = trace with { };
        clone.ShouldBe(trace);
        clone.ShouldNotBeSameAs(trace);
    }

    [Fact]
    public void Equality_WhenValuesMatch_AreStructurallyEqual()
    {
        var at = DateTimeOffset.UnixEpoch;
        var first = new NetworkOperationTrace(Id(), Destination(), "send", at);
        var second = new NetworkOperationTrace(Id(), Destination(), "send", at);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ToString().ShouldContain("send");
    }

    private static NetworkOperationId Id() => new(Guid.Parse("60000000-0000-0000-0000-000000000006"));
    private static NetworkDestination Destination() => new("https", new NormalizedHost("example.test"), 443, NetworkRoute.Root);
}
