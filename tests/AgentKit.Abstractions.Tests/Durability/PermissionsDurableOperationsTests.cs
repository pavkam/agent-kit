// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies the approval-wait boundary name and version are stable configuration values.</summary>
public sealed class PermissionsDurableOperationsTests
{
    [Fact]
    public void ApprovalWait_WhenRead_IsTheStableBoundaryName() =>
        PermissionsDurableOperations.ApprovalWait.Value.ShouldBe("agentkit.permissions.approval_wait");

    [Fact]
    public void ApprovalWaitVersion_WhenRead_IsExplicitlyPublished() =>
        PermissionsDurableOperations.ApprovalWaitVersion.Value.ShouldBe("v1");

    [Fact]
    public void All_WhenRead_ContainsEveryJournaledBoundaryExactlyOnce() =>
        PermissionsDurableOperations.All.ShouldBe([PermissionsDurableOperations.ApprovalWait]);
}
