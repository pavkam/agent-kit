// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies IdentityValidationPassed behavior and contracts.</summary>
public sealed class IdentityValidationPassedTests
{
    [Fact]
    public void IdentityValidationPassed_Instance_IsShared() => IdentityValidationPassed.Instance.ShouldBeSameAs(IdentityValidationPassed.Instance);
}
