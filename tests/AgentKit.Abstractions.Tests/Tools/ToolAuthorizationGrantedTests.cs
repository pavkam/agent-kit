// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolAuthorizationGranted behavior and contracts.</summary>
public sealed class ToolAuthorizationGrantedTests
{
    [Fact]
    public void ToolAuthorizationGranted_Equality_WhenBothInstances_AreEqual()
    {
        new ToolAuthorizationGranted().ShouldBe(new ToolAuthorizationGranted());
        new ToolAuthorizationGranted().GetHashCode().ShouldBe(new ToolAuthorizationGranted().GetHashCode());
    }
}
