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
    /// <exception cref="ArgumentNullException"><paramref name="tenantId"/> or <paramref name="principalId"/> is default.</exception>
    /// <exception cref="ArgumentException"><paramref name="tenantId"/> or <paramref name="principalId"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="agentId"/> is default, or a present optional identity is default.
    /// </exception>
    public BudgetScopeAddress(
        TenantId tenantId,
        PrincipalId principalId,
        AgentId agentId,
        SessionId? sessionId,
        RunId? runId,
        OperationId? operationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(principalId.Value, nameof(principalId));
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default, nameof(agentId));
        if (sessionId is { } session)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(session, default, nameof(sessionId));
        }

        if (runId is { } run)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(run, default, nameof(runId));
        }

        if (operationId is { } operation)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(operation, default, nameof(operationId));
        }

        TenantId = tenantId;
        PrincipalId = principalId;
        AgentId = agentId;
        SessionId = sessionId;
        RunId = runId;
        OperationId = operationId;
    }

    /// <summary>Gets the tenant this scope belongs to.</summary>
    /// <value>The required non-blank authenticated tenant partition.</value>
    /// <exception cref="ArgumentNullException">An initializer assigns a default value.</exception>
    /// <exception cref="ArgumentException">An initializer assigns blank tenant text.</exception>
    public TenantId TenantId
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value.Value, "tenantId");
            field = value;
        }
    }

    /// <summary>Gets the principal this scope belongs to.</summary>
    /// <value>The required non-blank authenticated principal.</value>
    /// <exception cref="ArgumentNullException">An initializer assigns a default value.</exception>
    /// <exception cref="ArgumentException">An initializer assigns blank principal text.</exception>
    public PrincipalId PrincipalId
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value.Value, "principalId");
            field = value;
        }
    }

    /// <summary>Gets the agent this scope belongs to.</summary>
    /// <value>The required nondefault owning agent.</value>
    /// <exception cref="ArgumentOutOfRangeException">An initializer assigns a default value.</exception>
    public AgentId AgentId
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, "agentId");
            field = value;
        }
    }

    /// <summary>Gets the session this scope is bound to, when applicable.</summary>
    /// <value>A nondefault session identity, or <see langword="null"/> for an address above session scope.</value>
    /// <exception cref="ArgumentOutOfRangeException">An initializer assigns a present default value.</exception>
    public SessionId? SessionId
    {
        get;
        init
        {
            if (value is { } id)
            {
                ArgumentOutOfRangeException.ThrowIfEqual(id, default, "sessionId");
            }

            field = value;
        }
    }

    /// <summary>Gets the run this scope is bound to, when applicable.</summary>
    /// <value>A nondefault active run identity, or <see langword="null"/> for work outside an active run.</value>
    /// <exception cref="ArgumentOutOfRangeException">An initializer assigns a present default value.</exception>
    public RunId? RunId
    {
        get;
        init
        {
            if (value is { } id)
            {
                ArgumentOutOfRangeException.ThrowIfEqual(id, default, "runId");
            }

            field = value;
        }
    }

    /// <summary>Gets the operation this scope is bound to, when applicable.</summary>
    /// <value>A nondefault operation identity, or <see langword="null"/> for a reusable parent scope.</value>
    /// <exception cref="ArgumentOutOfRangeException">An initializer assigns a present default value.</exception>
    public OperationId? OperationId
    {
        get;
        init
        {
            if (value is { } id)
            {
                ArgumentOutOfRangeException.ThrowIfEqual(id, default, "operationId");
            }

            field = value;
        }
    }
}
