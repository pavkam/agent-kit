// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Maps the interactive CodingAgent permission mode onto normalized protected effects.</summary>
internal sealed class CodingAgentSecurityPolicy(PermissionModeController permissions): ISecurityPolicy
{
    /// <inheritdoc/>
    public ValueTask<SecurityPolicyResult> EvaluateAsync(SecurityRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(DecisionFor(permissions.Mode, request.Kind, request.Effect));
    }

    /// <summary>Returns the policy contribution for one normalized operation without inspecting tool identity or arguments.</summary>
    /// <param name="mode">The active application permission mode.</param>
    /// <param name="kind">The protected operation kind.</param>
    /// <param name="effect">The material protected effect.</param>
    /// <returns>An allow, denial, or approval requirement for the exact normalized effect.</returns>
    internal static SecurityPolicyResult DecisionFor(PermissionMode mode, SecurityOperationKind kind, SecurityEffect effect)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(mode);
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentOutOfRangeException.ThrowIfUndefined(effect);

        var workspaceMutation = kind is SecurityOperationKind.FileWrite or SecurityOperationKind.DirectoryCreate;
        var processExecution = kind == SecurityOperationKind.Process && effect == SecurityEffect.Execute;
        return !workspaceMutation && !processExecution
            ? new SecurityPolicyResult(SecurityPolicyResultKind.Allow, "coding-agent.observe", "The operation does not perform a protected workspace or process mutation.")
            : mode switch
            {
                PermissionMode.ReadOnly => new SecurityPolicyResult(SecurityPolicyResultKind.Deny, "coding-agent.read-only", "Read-only mode denies workspace mutations and process execution."),
                PermissionMode.AutoApproveWorkspaceEdits when workspaceMutation => new SecurityPolicyResult(SecurityPolicyResultKind.Allow, "coding-agent.workspace-edit", "This mode allows exact workspace edits without an interactive prompt."),
                PermissionMode.AskForChanges or PermissionMode.AutoApproveWorkspaceEdits => new SecurityPolicyResult(SecurityPolicyResultKind.RequireApproval, "coding-agent.approval", "This exact protected operation requires interactive approval."),
                _ => throw new InvalidOperationException("The permission mode is not supported."),
            };
    }
}
