// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

/// <summary>Defines allocation-efficient, content-free budget log events.</summary>
internal static partial class BudgetLog
{
    /// <summary>Records the stable identity and idempotent outcome of a successfully resolved scope-creation request.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="budgetScopeId">The created or previously created scope identity.</param>
    /// <param name="outcome">The bounded creation outcome.</param>
    [LoggerMessage(7000, LogLevel.Information, "Budget scope {BudgetScopeId} completed creation with outcome {Outcome}.")]
    internal static partial void ScopeCreated(ILogger logger, BudgetScopeId budgetScopeId, string outcome);

    /// <summary>Records a typed scope-creation rejection without including request limits or identities.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="reason">The bounded failure-kind name.</param>
    [LoggerMessage(7001, LogLevel.Warning, "Budget scope creation was rejected with reason {Reason}.")]
    internal static partial void ScopeCreationRejected(ILogger logger, string reason);

    /// <summary>Records cancellation before scope creation reached a terminal mutation.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    [LoggerMessage(7002, LogLevel.Debug, "Budget scope creation was cancelled.")]
    internal static partial void ScopeCreationCancelled(ILogger logger);

    /// <summary>Records an unexpected scope-creation exception for diagnostic providers.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="errorType">The exception type raised by the authority.</param>
    [LoggerMessage(7003, LogLevel.Error, "Budget scope creation failed with error type {ErrorType}.")]
    internal static partial void ScopeCreationFailed(ILogger logger, string errorType);

    /// <summary>Records one terminal reservation decision using only scope, dimension, and bounded outcome fields.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="budgetScopeId">The scope against which capacity was requested.</param>
    /// <param name="budgetDimension">The registered dimension that was reserved or rejected.</param>
    /// <param name="outcome">The bounded reservation outcome.</param>
    [LoggerMessage(7010, LogLevel.Debug, "Budget reservation for scope {BudgetScopeId} dimension {BudgetDimension} completed with outcome {Outcome}.")]
    internal static partial void ReservationCompleted(
        ILogger logger, BudgetScopeId budgetScopeId, BudgetDimension budgetDimension, string outcome);

    /// <summary>Records caller cancellation of a capacity reservation before completion.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="budgetScopeId">The scope against which capacity was requested.</param>
    /// <param name="budgetDimension">The registered dimension being reserved.</param>
    [LoggerMessage(7011, LogLevel.Debug, "Budget reservation for scope {BudgetScopeId} dimension {BudgetDimension} was cancelled.")]
    internal static partial void ReservationCancelled(
        ILogger logger, BudgetScopeId budgetScopeId, BudgetDimension budgetDimension);

    /// <summary>Records an unexpected reservation exception without request amounts or idempotency values.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="budgetScopeId">The scope against which capacity was requested.</param>
    /// <param name="budgetDimension">The registered dimension being reserved.</param>
    /// <param name="errorType">The exception type raised by the reservation operation.</param>
    [LoggerMessage(7012, LogLevel.Error, "Budget reservation for scope {BudgetScopeId} dimension {BudgetDimension} failed with error type {ErrorType}.")]
    internal static partial void ReservationFailed(
        ILogger logger, BudgetScopeId budgetScopeId, BudgetDimension budgetDimension, string errorType);

    /// <summary>Records commitment or release of one reservation without amounts or idempotency values.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="budgetScopeId">The scope whose reserved capacity was settled.</param>
    /// <param name="budgetDimension">The registered dimension whose capacity was settled.</param>
    /// <param name="outcome">The bounded settlement outcome.</param>
    [LoggerMessage(7020, LogLevel.Debug, "Budget settlement for scope {BudgetScopeId} dimension {BudgetDimension} completed with outcome {Outcome}.")]
    internal static partial void SettlementCompleted(
        ILogger logger, BudgetScopeId budgetScopeId, BudgetDimension budgetDimension, string outcome);

    /// <summary>Records an unexpected settlement exception without capacity or idempotency values.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="budgetScopeId">The scope whose reserved capacity was being settled.</param>
    /// <param name="budgetDimension">The registered dimension whose capacity was being settled.</param>
    /// <param name="errorType">The exception type raised during settlement.</param>
    [LoggerMessage(7021, LogLevel.Error, "Budget settlement for scope {BudgetScopeId} dimension {BudgetDimension} failed with error type {ErrorType}.")]
    internal static partial void SettlementFailed(
        ILogger logger, BudgetScopeId budgetScopeId, BudgetDimension budgetDimension, string errorType);
}
