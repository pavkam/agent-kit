// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionLocationResult behavior and contracts.</summary>
public sealed class SessionLocationResultTests
{
    [Fact]
    public void SessionLocated_WhenLocationIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionLocated(null!));
        exception.ParamName.ShouldBe("location");
    }

    [Fact]
    public void SessionLocated_With_WhenApplied_ProducesEqualCopy()
    {
        var location = SessionsTestData.Location();
        var original = new SessionLocated(location);
        var copy = original with { };
        copy.ShouldBe(original);
        original.Location.ShouldBe(location);
    }

    [Fact]
    public void SessionLocationNotFound_WhenAddressIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionLocationNotFound(null!));
        exception.ParamName.ShouldBe("address");
    }

    [Fact]
    public void SessionLocationNotFound_With_WhenApplied_ProducesEqualCopy()
    {
        var address = SessionsTestData.Address();
        var original = new SessionLocationNotFound(address);
        var copy = original with { };
        copy.ShouldBe(original);
        original.Address.ShouldBe(address);
    }
}
