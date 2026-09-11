// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;



/// <summary>Verifies SessionStoreSelectionRejected behavior and contracts.</summary>
public sealed class SessionStoreSelectionRejectedTests
{
    [Fact]
    public void SessionStoreSelectionRejected_WhenReasonIsUndefined_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SessionStoreSelectionRejected((SessionStoreSelectionRejectionReason) 42, "safe"));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("reason");
    }
}
