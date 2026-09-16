// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionCreationLocationResult behavior and contracts.</summary>
public sealed class SessionCreationLocationResultTests
{
    [Fact]
    public void SessionCreationLocationLocated_WhenLocationIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionCreationLocationLocated(null!));
        exception.ParamName.ShouldBe("location");
    }

    [Fact]
    public void SessionCreationLocationLocated_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var location = SessionsTestData.Location();
        var result = new SessionCreationLocationLocated(location);
        result.Location.ShouldBe(location);
    }

    [Fact]
    public void SessionCreationLocationLocated_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionCreationLocationLocated(SessionsTestData.Location());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void SessionCreationLocationNotFound_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionCreationLocationNotFound();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void SessionDirectoryCreationLookupDenied_WhenSafeMessageIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SessionDirectoryCreationLookupDenied(" ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void SessionDirectoryCreationLookupDenied_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionDirectoryCreationLookupDenied("denied");
        var copy = original with { };
        copy.ShouldBe(original);
        original.SafeMessage.ShouldBe("denied");
    }

    [Fact]
    public void SessionDirectoryCreationLookupUnavailable_WhenSafeMessageIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SessionDirectoryCreationLookupUnavailable(" ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void SessionDirectoryCreationLookupUnavailable_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionDirectoryCreationLookupUnavailable("unavailable");
        var copy = original with { };
        copy.ShouldBe(original);
        original.SafeMessage.ShouldBe("unavailable");
    }

    [Fact]
    public void SessionCreationLocationResult_WhenPatternMatched_DiscriminatesDerivedKinds()
    {
        SessionCreationLocationResult located = new SessionCreationLocationLocated(SessionsTestData.Location());
        SessionCreationLocationResult notFound = new SessionCreationLocationNotFound();
        _ = located.ShouldBeOfType<SessionCreationLocationLocated>();
        _ = notFound.ShouldBeOfType<SessionCreationLocationNotFound>();
    }
}
