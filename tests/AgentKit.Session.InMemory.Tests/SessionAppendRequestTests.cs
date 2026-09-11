// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;
/// <summary>Verifies SessionAppendRequest behavior and contracts.</summary>
public sealed class SessionAppendRequestTests
{
    [Fact]
    public void SessionAppendRequest_WhenEntriesEmpty_ThrowsArgumentException()
    {
        var context = TestFactory.OperationContext(new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid())));
        var exception = Should.Throw<ArgumentException>(() => new SessionAppendRequest(context, new BranchId(Guid.NewGuid()), new SessionVersion(0), new IdempotencyKey("x"), []));
        exception.ParamName.ShouldBe("entries");
    }
}
