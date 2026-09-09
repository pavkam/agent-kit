// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite;

/// <summary>Defines stable content-free terminal events for SQLite budget-ledger operations.</summary>
internal static partial class SqliteBudgetLedgerLog
{
    /// <summary>Records one terminal semantic outcome with safe correlation identities.</summary>
    /// <param name="logger">The configured logger.</param><param name="budgetOperation">The bounded operation.</param>
    /// <param name="outcome">The bounded outcome.</param><param name="tenantId">The tenant, when known.</param>
    /// <param name="principalId">The principal, when known.</param><param name="agentId">The agent, when known.</param>
    /// <param name="sessionId">The session, when known.</param><param name="runId">The run, when known.</param>
    /// <param name="budgetScopeId">The scope, when known.</param><param name="budgetReservationId">The reservation, when known.</param><param name="operationId">The domain operation, when known.</param>
    [LoggerMessage(7070, LogLevel.Debug, "SQLite budget ledger operation {BudgetOperation} completed with outcome {Outcome} for tenant {TenantId}, principal {PrincipalId}, agent {AgentId}, session {SessionId}, run {RunId}, scope {BudgetScopeId}, reservation {BudgetReservationId}, and operation {OperationId}.")]
    internal static partial void Completed(ILogger logger, string budgetOperation, string outcome, string? tenantId, string? principalId, string? agentId, string? sessionId, string? runId, string? budgetScopeId, string? budgetReservationId, string? operationId);

    /// <summary>Records one terminal exception without request or persisted content.</summary>
    /// <param name="logger">The configured logger.</param><param name="budgetOperation">The bounded operation.</param>
    /// <param name="outcome">The bounded cancelled or faulted outcome.</param><param name="errorType">The normalized exception type.</param>
    /// <param name="tenantId">The tenant, when known.</param><param name="principalId">The principal, when known.</param><param name="agentId">The agent, when known.</param>
    /// <param name="sessionId">The session, when known.</param><param name="runId">The run, when known.</param>
    /// <param name="budgetScopeId">The scope, when known.</param><param name="budgetReservationId">The reservation, when known.</param><param name="operationId">The domain operation, when known.</param>
    [LoggerMessage(7071, LogLevel.Error, "SQLite budget ledger operation {BudgetOperation} ended with outcome {Outcome} and error type {ErrorType} for tenant {TenantId}, principal {PrincipalId}, agent {AgentId}, session {SessionId}, run {RunId}, scope {BudgetScopeId}, reservation {BudgetReservationId}, and operation {OperationId}.")]
    internal static partial void Failed(ILogger logger, string budgetOperation, string outcome, string errorType, string? tenantId, string? principalId, string? agentId, string? sessionId, string? runId, string? budgetScopeId, string? budgetReservationId, string? operationId);
}
