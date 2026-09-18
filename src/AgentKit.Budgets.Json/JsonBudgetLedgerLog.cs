// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json;

/// <summary>Defines stable content-free source-generated events for the durable JSON budget-ledger adapter.</summary>
/// <remarks>
/// Events carry only bounded operation and outcome names plus applicable correlation identities. Reserved amounts, actual
/// usage, limit values, replay keys, and the configured store root are accounting content or sensitive bootstrap
/// configuration and are never emitted.
/// </remarks>
internal static partial class JsonBudgetLedgerLog
{
    /// <summary>Records one terminal semantic ledger result.</summary>
    /// <param name="logger">The content-free logger.</param>
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
    [LoggerMessage(19200, LogLevel.Debug, "JSON budget ledger operation {BudgetOperation} completed with outcome {Outcome} for tenant {TenantId}, principal {PrincipalId}, agent {AgentId}, session {SessionId}, run {RunId}, scope {BudgetScopeId}, reservation {BudgetReservationId}, and operation {OperationId}.")]
    internal static partial void OperationCompleted(
        ILogger logger,
        string budgetOperation,
        string outcome,
        string? tenantId,
        string? principalId,
        string? agentId,
        string? sessionId,
        string? runId,
        string? budgetScopeId,
        string? budgetReservationId,
        string? operationId);

    /// <summary>Records one non-successful terminal ledger observation without persisted or protected content.</summary>
    /// <param name="logger">The content-free logger.</param>
    /// <param name="budgetOperation">The bounded operation name.</param>
    /// <param name="outcome">The bounded terminal outcome.</param>
    /// <param name="failureKind">The normalized bounded failure class.</param>
    /// <param name="tenantId">The applicable tenant identity.</param>
    /// <param name="principalId">The applicable principal identity.</param>
    /// <param name="agentId">The applicable agent identity.</param>
    /// <param name="sessionId">The applicable session identity, when present.</param>
    /// <param name="runId">The applicable run identity, when present.</param>
    /// <param name="budgetScopeId">The applicable scope identity, when established.</param>
    /// <param name="budgetReservationId">The applicable reservation identity, when established.</param>
    /// <param name="operationId">The applicable logical operation identity, when established.</param>
    [LoggerMessage(19201, LogLevel.Warning, "JSON budget ledger operation {BudgetOperation} completed with outcome {Outcome} and failure kind {FailureKind} for tenant {TenantId}, principal {PrincipalId}, agent {AgentId}, session {SessionId}, run {RunId}, scope {BudgetScopeId}, reservation {BudgetReservationId}, and operation {OperationId}.")]
    internal static partial void OperationFailed(
        ILogger logger,
        string budgetOperation,
        string outcome,
        string failureKind,
        string? tenantId,
        string? principalId,
        string? agentId,
        string? sessionId,
        string? runId,
        string? budgetScopeId,
        string? budgetReservationId,
        string? operationId);
}
