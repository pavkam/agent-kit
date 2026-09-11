// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

/// <summary>Verifies SessionRetentionDecision behavior and contracts.</summary>
public sealed class SessionRetentionDecisionTests
{
    [Fact]
    public void SessionRetentionDecision_Equality_WhenSameValues_InstancesAreEqual() => new SessionRetentionDecision(SessionRetentionAction.Delete, "old").ShouldBe(new SessionRetentionDecision(SessionRetentionAction.Delete, "old"));
}
