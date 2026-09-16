// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

/// <summary>Verifies SessionAppendConflict behavior and contracts.</summary>
public sealed class SessionAppendConflictTests
{
    [Fact]
    public void SessionAppendConflict_Equality_WhenSameValues_InstancesAreEqual() => new SessionAppendConflict(new SessionVersion(1), new SessionVersion(2)).ShouldBe(new SessionAppendConflict(new SessionVersion(1), new SessionVersion(2)));

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionAppendConflict(new SessionVersion(1), new SessionVersion(2));
        var copy = original with { };
        copy.ShouldBe(original);
    }

}
