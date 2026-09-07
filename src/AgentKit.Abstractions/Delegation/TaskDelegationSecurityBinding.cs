// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Security.Cryptography;

/// <summary>Produces exact security resources and fingerprints for delegated child creation.</summary>
public static class TaskDelegationSecurityBinding
{
    /// <summary>Names the requested child creation resource.</summary>
    /// <param name="id">The delegation identity.</param>
    /// <returns>The canonical protected resource.</returns>
    public static ProtectedResource Resource(DelegationId id) => new(ProtectedResourceKind.Delegation, $"delegation:{id}");

    /// <summary>Fingerprints every behaviorally meaningful child envelope field.</summary>
    /// <param name="prompt">The validated child envelope.</param>
    /// <returns>A deterministic SHA-256 fingerprint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="prompt"/> is null.</exception>
    public static InputFingerprint Fingerprint(TaskDelegationPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            id = prompt.Id.ToString(),
            parentAgentId = prompt.ParentAgentId.ToString(),
            parentSessionId = prompt.ParentSessionId.ToString(),
            parentRunId = prompt.ParentRunId.ToString(),
            operationId = prompt.Correlation.OperationId.ToString(),
            turnId = prompt.Correlation.TurnId?.ToString(),
            toolCallId = prompt.ToolCallId.ToString(),
            targetAgentId = prompt.TargetAgentId.ToString(),
            prompt.Objective,
            acceptanceCriteria = prompt.AcceptanceCriteria,
            allowedTools = prompt.AllowedTools.Select(static tool => tool.Value),
            prompt.Budget.MaximumTurns,
            prompt.Budget.MaximumToolCalls,
            deadline = prompt.Deadline.ToUniversalTime().ToString("O"),
        });
        return new InputFingerprint(Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }
}
