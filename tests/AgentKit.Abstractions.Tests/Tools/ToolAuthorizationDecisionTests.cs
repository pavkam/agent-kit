// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolAuthorizationDecision behavior and contracts.</summary>
public sealed class ToolAuthorizationDecisionTests
{
    [Fact]
    public void ToolAuthorizationDecision_Hierarchy_EveryLeafDerivesFromToolAuthorizationDecision()
    {
        ToolAuthorizationDecision granted = new ToolAuthorizationGranted();
        ToolAuthorizationDecision denied = new ToolAuthorizationDenied("no");
        _ = granted.ShouldBeOfType<ToolAuthorizationGranted>();
        _ = denied.ShouldBeOfType<ToolAuthorizationDenied>();
    }
}
