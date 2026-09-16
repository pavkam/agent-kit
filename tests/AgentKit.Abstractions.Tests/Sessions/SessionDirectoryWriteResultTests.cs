// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionDirectoryWriteResult behavior and contracts.</summary>
public sealed class SessionDirectoryWriteResultTests
{
    [Fact]
    public void SessionLocationConflict_WhenExistingIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionLocationConflict(null!, new SessionStoreKey("store")));
        exception.ParamName.ShouldBe("existing");
    }

    [Fact]
    public void SessionLocationConflict_WhenRequestedStoreKeyIsDefault_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionLocationConflict(SessionsTestData.Location(), default));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("requestedStoreKey");
    }

    [Fact]
    public void SessionLocationConflict_With_WhenApplied_ProducesEqualCopy()
    {
        var location = SessionsTestData.Location();
        var original = new SessionLocationConflict(location, new SessionStoreKey("other"));
        var copy = original with { };
        copy.ShouldBe(original);
        original.Existing.ShouldBe(location);
        original.RequestedStoreKey.ShouldBe(new SessionStoreKey("other"));
    }

    [Fact]
    public void SessionLocationRecorded_WhenLocationIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionLocationRecorded(null!, existing: false));
        exception.ParamName.ShouldBe("location");
    }

    [Fact]
    public void SessionLocationRecorded_With_WhenApplied_ProducesEqualCopy()
    {
        var location = SessionsTestData.Location();
        var original = new SessionLocationRecorded(location, existing: true);
        var copy = original with { };
        copy.ShouldBe(original);
        original.Location.ShouldBe(location);
        original.Existing.ShouldBeTrue();
    }

    [Fact]
    public void SessionDirectoryWriteDenied_WhenSafeMessageIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SessionDirectoryWriteDenied(" ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void SessionDirectoryWriteDenied_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionDirectoryWriteDenied("denied");
        var copy = original with { };
        copy.ShouldBe(original);
        original.SafeMessage.ShouldBe("denied");
    }

    [Fact]
    public void SessionDirectoryWriteUnavailable_WhenSafeMessageIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SessionDirectoryWriteUnavailable(" ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void SessionDirectoryWriteUnavailable_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionDirectoryWriteUnavailable("unavailable");
        var copy = original with { };
        copy.ShouldBe(original);
        original.SafeMessage.ShouldBe("unavailable");
    }
}
