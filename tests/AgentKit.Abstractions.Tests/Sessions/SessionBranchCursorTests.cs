// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionBranchCursor behavior and contracts.</summary>
public sealed class SessionBranchCursorTests
{
    [Fact]
    public void Constructor_WhenBranchIdIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SessionBranchCursor(default, null));
        exception.ParamName.ShouldBe("branchId");
    }

    [Fact]
    public void Constructor_WhenLastEntryIdIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SessionBranchCursor(SessionsTestData.BranchId, default(SessionEntryId)));
        exception.ParamName.ShouldBe("lastEntryId");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var cursor = new SessionBranchCursor(SessionsTestData.BranchId, SessionsTestData.EntryId);
        cursor.BranchId.ShouldBe(SessionsTestData.BranchId);
        cursor.LastEntryId.ShouldBe(SessionsTestData.EntryId);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionBranchCursor(SessionsTestData.BranchId, null);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
