// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.InMemory;

/// <summary>Defines stable content-free events for authoritative budget-ledger operations.</summary>
internal static partial class BudgetLedgerLog
{
    /// <summary>Records one terminal semantic ledger result.</summary>
    /// <param name="logger">The configured logger.</param>
    /// <param name="budgetOperation">The bounded operation name.</param>
    /// <param name="outcome">The bounded semantic outcome.</param>
    /// <param name="tenantId">The applicable tenant identity.</param>
    /// <param name="principalId">The applicable principal identity.</param>
    /// <param name="agentId">The applicable agent identity.</param>
    /// <param name="sessionId">The applicable session identity, when present.</param>
    /// <param name="runId">The applicable run identity, when present.</param>
    /// <param name="budgetScopeId">The applicable scope identity, when established.</param>
    /// <param name="budgetReservationId">The applicable reservation identity, when established.</param>
    /// <param name="operationId">The applicable logical operation identity, when established.</param>
    [LoggerMessage(7050, LogLevel.Debug, "Budget ledger operation {BudgetOperation} completed with outcome {Outcome} for tenant {TenantId}, principal {PrincipalId}, agent {AgentId}, session {SessionId}, run {RunId}, scope {BudgetScopeId}, reservation {BudgetReservationId}, and operation {OperationId}.")]
    internal static partial void Completed(ILogger logger, string budgetOperation, string outcome, string? tenantId, string? principalId, string? agentId, string? sessionId, string? runId, string? budgetScopeId, string? budgetReservationId, string? operationId);

    /// <summary>Records one ledger exception with applicable correlation identities and without request content.</summary>
    /// <param name="logger">The configured logger.</param>
    /// <param name="budgetOperation">The bounded operation name.</param>
    /// <param name="errorType">The normalized exception type.</param>
    /// <param name="tenantId">The applicable tenant identity.</param>
    /// <param name="principalId">The applicable principal identity.</param>
    /// <param name="agentId">The applicable agent identity.</param>
    /// <param name="sessionId">The applicable session identity, when present.</param>
    /// <param name="runId">The applicable run identity, when present.</param>
    /// <param name="budgetScopeId">The applicable scope identity, when established.</param>
    /// <param name="budgetReservationId">The applicable reservation identity, when established.</param>
    /// <param name="operationId">The applicable logical operation identity, when established.</param>
    [LoggerMessage(7051, LogLevel.Error, "Budget ledger operation {BudgetOperation} failed with error type {ErrorType} for tenant {TenantId}, principal {PrincipalId}, agent {AgentId}, session {SessionId}, run {RunId}, scope {BudgetScopeId}, reservation {BudgetReservationId}, and operation {OperationId}.")]
    internal static partial void Failed(ILogger logger, string budgetOperation, string errorType, string? tenantId, string? principalId, string? agentId, string? sessionId, string? runId, string? budgetScopeId, string? budgetReservationId, string? operationId);
}
