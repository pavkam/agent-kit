// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Storage;

/// <summary>Defines content-free structured logs emitted by every evaluation result-store adapter.</summary>
/// <remarks>Event identifiers 36100-36109 are owned by the shared evaluation result-store source. Logs carry identities and bounded vocabularies only, never result content.</remarks>
internal static partial class EvaluationResultStoreLog
{
    /// <summary>Records one terminal store operation.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="level">Information for served operations, Warning for refusals.</param>
    /// <param name="adapter">The bounded adapter name.</param>
    /// <param name="operation">The bounded operation name.</param>
    /// <param name="outcome">The bounded outcome.</param>
    /// <param name="runId">The evaluation run concerned.</param>
    [LoggerMessage(EventId = 36100, Message = "Evaluation result store {Adapter} operation {Operation} ended with {Outcome} for run {RunId}.")]
    internal static partial void Completed(ILogger logger, LogLevel level, string adapter, string operation, string outcome, EvaluationRunId runId);

    /// <summary>Records caller cancellation of a store operation.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="adapter">The bounded adapter name.</param>
    /// <param name="operation">The bounded operation name.</param>
    /// <param name="runId">The evaluation run concerned.</param>
    [LoggerMessage(EventId = 36101, Level = LogLevel.Information, Message = "Evaluation result store {Adapter} operation {Operation} was cancelled for run {RunId}.")]
    internal static partial void Cancelled(ILogger logger, string adapter, string operation, EvaluationRunId runId);

    /// <summary>Records an unexpected store failure by type only.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="adapter">The bounded adapter name.</param>
    /// <param name="operation">The bounded operation name.</param>
    /// <param name="runId">The evaluation run concerned.</param>
    /// <param name="errorType">The normalized exception type, never raw exception content.</param>
    [LoggerMessage(EventId = 36102, Level = LogLevel.Error, Message = "Evaluation result store {Adapter} operation {Operation} failed with {ErrorType} for run {RunId}.")]
    internal static partial void Faulted(ILogger logger, string adapter, string operation, EvaluationRunId runId, string errorType);
}
