// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionInputPromotionResult derived behavior and contracts.</summary>
public sealed class SessionInputPromotionResultTests
{
    [Fact]
    public void SessionInputPromoted_WhenPromotedIsEmpty_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new SessionInputPromoted(
            [], SessionsTestData.Cursor(), new SessionVersion(1), new OperationStateRevision(1)));
        exception.ParamName.ShouldBe("promoted");
    }

    [Fact]
    public void SessionInputPromoted_WhenAnEntryIsNotPromoted_ThrowsExactArgumentException()
    {
        var notPromoted = SessionsTestData.AdmittedInput();
        var exception = Should.Throw<ArgumentException>(() => new SessionInputPromoted(
            [notPromoted], SessionsTestData.Cursor(), new SessionVersion(1), new OperationStateRevision(1)));
        exception.ParamName.ShouldBe("promoted");
    }

    [Fact]
    public void SessionInputPromoted_WhenBranchCursorIsNull_ThrowsExactArgumentNullException()
    {
        var promoted = Promoted();
        var exception = Should.Throw<ArgumentNullException>(
            () => new SessionInputPromoted([promoted], null!, new SessionVersion(1), new OperationStateRevision(1)));
        exception.ParamName.ShouldBe("committedCursor");
    }

    [Fact]
    public void SessionInputPromoted_WhenOperationStateRevisionIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var promoted = Promoted();
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new SessionInputPromoted([promoted], SessionsTestData.Cursor(), new SessionVersion(1), default));
        exception.ParamName.ShouldBe("operationStateRevision");
    }

    [Fact]
    public void SessionInputPromoted_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var promoted = Promoted();
        var cursor = SessionsTestData.Cursor();
        var result = new SessionInputPromoted([promoted], cursor, new SessionVersion(5), new OperationStateRevision(2));

        result.Promoted.ShouldBe([promoted]);
        result.CommittedCursor.ShouldBe(cursor);
        result.SessionVersion.ShouldBe(new SessionVersion(5));
        result.OperationStateRevision.ShouldBe(new OperationStateRevision(2));
    }

    [Fact]
    public void SessionInputPromoted_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionInputPromoted(
            [Promoted()], SessionsTestData.Cursor(), new SessionVersion(1), new OperationStateRevision(1));
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void SessionInputPromotionRejected_WhenSafeReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SessionInputPromotionRejected(" ")).ParamName.ShouldBe("safeReason");

    [Fact]
    public void SessionInputPromotionRejected_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionInputPromotionRejected("rejected");
        var copy = original with { };
        copy.ShouldBe(original);
        original.SafeReason.ShouldBe("rejected");
    }

    private static AdmittedInput Promoted() =>
        new(SessionsTestData.AdmissionId, SessionsTestData.AgentId, SessionsTestData.SessionId, SessionsTestData.LaneId,
            SessionsTestData.Identity(), new SessionSequence(1), SessionsTestData.Input(), SessionsTestData.Input(),
            SessionsTestData.Preprocessing(), DateTimeOffset.UnixEpoch, new SessionSequence(2));
}
