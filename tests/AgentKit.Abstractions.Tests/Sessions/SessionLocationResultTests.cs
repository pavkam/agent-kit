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

    [Fact]
    public void SessionDirectoryLookupDenied_WhenSafeMessageIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SessionDirectoryLookupDenied(" ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void SessionDirectoryLookupDenied_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionDirectoryLookupDenied("denied");
        var copy = original with { };
        copy.ShouldBe(original);
        original.SafeMessage.ShouldBe("denied");
    }

    [Fact]
    public void SessionDirectoryLookupUnavailable_WhenSafeMessageIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SessionDirectoryLookupUnavailable(" ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void SessionDirectoryLookupUnavailable_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionDirectoryLookupUnavailable("unavailable");
        var copy = original with { };
        copy.ShouldBe(original);
        original.SafeMessage.ShouldBe("unavailable");
    }
}
