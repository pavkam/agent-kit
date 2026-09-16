// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

/// <summary>Verifies SessionBranched behavior and contracts.</summary>
public sealed class SessionBranchedTests
{
    private static readonly Guid _branchGuid = Guid.Parse("33333333-3333-3333-3333-333333333333");
    [Fact]
    public void SessionBranched_Equality_WhenSameValues_InstancesAreEqual() => new SessionBranched(BranchId, new SessionSequence(1)).ShouldBe(new SessionBranched(BranchId, new SessionSequence(1)));

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionBranched(BranchId, new SessionSequence(1));
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static BranchId BranchId => new(_branchGuid);
}
