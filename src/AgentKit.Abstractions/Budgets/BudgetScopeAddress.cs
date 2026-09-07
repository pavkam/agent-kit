// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The hierarchical address of one budget scope within the shared ledger:
/// host, tenant, principal, agent, session, run, and operation.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. Tenant,
/// agent, session, run, and operation are ledger addresses inside one
/// shared authority, not separate authority instances; a scope's address is
/// what lets consumption and enforcement compose correctly across those
/// levels.
/// </remarks>
public sealed record BudgetScopeAddress
{
    /// <summary>Initializes a new instance of the <see cref="BudgetScopeAddress"/> record.</summary>
    /// <param name="tenantId">The tenant this scope belongs to.</param>
    /// <param name="principalId">The principal this scope belongs to.</param>
    /// <param name="agentId">The agent this scope belongs to.</param>
    /// <param name="sessionId">The session this scope is bound to, when applicable.</param>
    /// <param name="runId">The run this scope is bound to, when applicable.</param>
    /// <param name="operationId">The operation this scope is bound to, when applicable.</param>
    public BudgetScopeAddress(
        TenantId tenantId,
        PrincipalId principalId,
        AgentId agentId,
        SessionId? sessionId,
        RunId? runId,
        OperationId? operationId)
    {
        TenantId = tenantId;
        PrincipalId = principalId;
        AgentId = agentId;
        SessionId = sessionId;
        RunId = runId;
        OperationId = operationId;
    }

    /// <summary>Gets the tenant this scope belongs to.</summary>
    public TenantId TenantId { get; init; }

    /// <summary>Gets the principal this scope belongs to.</summary>
    public PrincipalId PrincipalId { get; init; }

    /// <summary>Gets the agent this scope belongs to.</summary>
    public AgentId AgentId { get; init; }

    /// <summary>Gets the session this scope is bound to, when applicable.</summary>
    public SessionId? SessionId { get; init; }

    /// <summary>Gets the run this scope is bound to, when applicable.</summary>
    public RunId? RunId { get; init; }

    /// <summary>Gets the operation this scope is bound to, when applicable.</summary>
    public OperationId? OperationId { get; init; }
}
