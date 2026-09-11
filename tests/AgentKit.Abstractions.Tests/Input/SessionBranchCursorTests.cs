// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;



/// <summary>Verifies SessionBranchCursor behavior and contracts.</summary>
public sealed class SessionBranchCursorTests
{
    [Fact]
    public void SessionBranchCursor_WhenLastEntryIsDefault_ThrowsArgumentOutOfRangeExceptionWithParamName()
    {
        var exception = ShouldThrowExactly<ArgumentOutOfRangeException>(() => new SessionBranchCursor(Branch(), default(SessionEntryId)));
        exception.ParamName.ShouldBe("lastEntryId");
    }

    [Fact]
    public void SessionBranchCursor_WhenBranchIsDefault_ThrowsArgumentOutOfRangeExceptionWithParamName()
    {
        var exception = ShouldThrowExactly<ArgumentOutOfRangeException>(() => new SessionBranchCursor(default, null));
        exception.ParamName.ShouldBe("branchId");
    }

    [Fact]
    public void SessionBranchCursor_WhenBranchIsEmpty_RetainsANullTip()
    {
        var cursor = new SessionBranchCursor(Branch(), null);
        cursor.BranchId.ShouldBe(Branch());
        cursor.LastEntryId.ShouldBeNull();
    }

    private static BranchId Branch() => new(Guid.Parse("40000000-0000-0000-0000-000000000001"));

    private static TException ShouldThrowExactly<TException>(Func<object?> action)
        where TException : Exception
    {
        var exception = Should.Throw<Exception>(() => _ = action());
        exception.GetType().ShouldBe(typeof(TException));
        return (TException) exception;
    }
}
