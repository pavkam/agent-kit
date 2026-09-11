// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

/// <summary>Verifies SessionBranchParentNotFound behavior and contracts.</summary>
public sealed class SessionBranchParentNotFoundTests
{
    private static readonly Guid _branchGuid = Guid.Parse("33333333-3333-3333-3333-333333333333");
    [Fact]
    public void SessionBranchParentNotFound_Equality_WhenSameValues_InstancesAreEqual() => new SessionBranchParentNotFound(BranchId, new SessionSequence(1)).ShouldBe(new SessionBranchParentNotFound(BranchId, new SessionSequence(1)));
    private static BranchId BranchId => new(_branchGuid);
}
