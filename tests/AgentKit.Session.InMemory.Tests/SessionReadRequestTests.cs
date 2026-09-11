// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;



/// <summary>Verifies SessionReadRequest behavior and contracts.</summary>
public sealed class SessionReadRequestTests
{
    [Fact]
    public void SessionReadRequest_WhenPageSizeIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        var context = TestFactory.OperationContext(new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid())));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new SessionReadRequest(context, new BranchId(Guid.NewGuid()), new SessionSequence(0), 0));
    }
}
