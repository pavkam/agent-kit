// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent.Tests;

using AgentKit;

/// <summary>Verifies UI permission modes map to normalized security effects without tool-name gates.</summary>
public sealed class CodingAgentSecurityPolicyTests
{
    [Theory]
    [InlineData((int) PermissionMode.AskForChanges, SecurityOperationKind.FileWrite, SecurityEffect.Replace, SecurityPolicyResultKind.RequireApproval)]
    [InlineData((int) PermissionMode.AskForChanges, SecurityOperationKind.Process, SecurityEffect.Execute, SecurityPolicyResultKind.RequireApproval)]
    [InlineData((int) PermissionMode.ReadOnly, SecurityOperationKind.FileWrite, SecurityEffect.Create, SecurityPolicyResultKind.Deny)]
    [InlineData((int) PermissionMode.ReadOnly, SecurityOperationKind.Process, SecurityEffect.Execute, SecurityPolicyResultKind.Deny)]
    [InlineData((int) PermissionMode.AutoApproveWorkspaceEdits, SecurityOperationKind.FileWrite, SecurityEffect.CreateOrReplace, SecurityPolicyResultKind.Allow)]
    [InlineData((int) PermissionMode.AutoApproveWorkspaceEdits, SecurityOperationKind.Process, SecurityEffect.Execute, SecurityPolicyResultKind.RequireApproval)]
    public void DecisionFor_WhenOperationIsProtectedMutation_MapsConfiguredMode(
        int mode,
        SecurityOperationKind kind,
        SecurityEffect effect,
        SecurityPolicyResultKind expected)
    {
        var result = CodingAgentSecurityPolicy.DecisionFor((PermissionMode) mode, kind, effect);
        result.Kind.ShouldBe(expected);
    }

    [Theory]
    [InlineData(SecurityOperationKind.FileRead, SecurityEffect.Observe)]
    [InlineData(SecurityOperationKind.FileSearch, SecurityEffect.Observe)]
    [InlineData(SecurityOperationKind.Process, SecurityEffect.Observe)]
    [InlineData(SecurityOperationKind.StateMutation, SecurityEffect.Mutate)]
    public void DecisionFor_WhenOperationIsNotWorkspaceOrProcessMutation_AllowsRequiredSubeffect(
        SecurityOperationKind kind,
        SecurityEffect effect)
    {
        foreach (var mode in Enum.GetValues<PermissionMode>())
        {
            CodingAgentSecurityPolicy.DecisionFor(mode, kind, effect).Kind.ShouldBe(SecurityPolicyResultKind.Allow);
        }
    }
}
