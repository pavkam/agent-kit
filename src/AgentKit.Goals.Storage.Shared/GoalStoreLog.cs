// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Storage;

/// <summary>Defines content-free structured logs emitted by every goal-store adapter.</summary>
/// <remarks>Event identifiers 31100-31109 are owned by the shared goal-store source. Logs carry identities and bounded vocabularies only, never objectives, results, or prompts.</remarks>
internal static partial class GoalStoreLog
{
    /// <summary>Records one terminal goal-store operation.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="level">Information for served operations, Warning for refusals.</param>
    /// <param name="adapter">The bounded adapter name.</param>
    /// <param name="operation">The bounded operation name.</param>
    /// <param name="outcome">The bounded outcome.</param>
    /// <param name="goalId">The goal concerned, when known.</param>
    [LoggerMessage(EventId = 31100, Message = "Goal store {Adapter} operation {Operation} ended with {Outcome} for goal {GoalId}.")]
    internal static partial void Completed(ILogger logger, LogLevel level, string adapter, string operation, string outcome, GoalId? goalId);

    /// <summary>Records caller cancellation of a goal-store operation.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="adapter">The bounded adapter name.</param>
    /// <param name="operation">The bounded operation name.</param>
    /// <param name="goalId">The goal concerned, when known.</param>
    [LoggerMessage(EventId = 31101, Level = LogLevel.Information, Message = "Goal store {Adapter} operation {Operation} was cancelled for goal {GoalId}.")]
    internal static partial void Cancelled(ILogger logger, string adapter, string operation, GoalId? goalId);

    /// <summary>Records an unexpected goal-store failure by type only.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="adapter">The bounded adapter name.</param>
    /// <param name="operation">The bounded operation name.</param>
    /// <param name="goalId">The goal concerned, when known.</param>
    /// <param name="errorType">The normalized exception type, never raw exception content.</param>
    [LoggerMessage(EventId = 31102, Level = LogLevel.Error, Message = "Goal store {Adapter} operation {Operation} failed with {ErrorType} for goal {GoalId}.")]
    internal static partial void Faulted(ILogger logger, string adapter, string operation, GoalId? goalId, string errorType);
}
