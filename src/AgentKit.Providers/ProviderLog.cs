// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>
/// Defines allocation-efficient structured log events for model catalog
/// composition and selection.
/// </summary>
/// <remarks>
/// Every field logged here is bounded configuration evidence: catalog
/// versions, model counts, aliases, and normalized outcomes. Prompts, model
/// output, credentials, and endpoint secrets are never logged.
/// </remarks>
internal static partial class ProviderLog
{
    /// <summary>Logs the size and version of a newly composed catalog snapshot.</summary>
    [LoggerMessage(
        6100,
        LogLevel.Information,
        "Composed model catalog version {CatalogVersion} with {ModelCount} conversational models.")]
    internal static partial void CatalogComposed(
        ILogger logger,
        long catalogVersion,
        int modelCount);

    /// <summary>Logs the alias chosen for one model request.</summary>
    [LoggerMessage(
        6101,
        LogLevel.Debug,
        "Selected model {ModelAlias} for model request {ModelRequestId} from catalog version {CatalogVersion}.")]
    internal static partial void ModelSelected(
        ILogger logger,
        ModelAlias modelAlias,
        ModelRequestId modelRequestId,
        long catalogVersion);

    /// <summary>Logs that no configured candidate satisfied the request.</summary>
    [LoggerMessage(
        6102,
        LogLevel.Warning,
        "No compatible model for model request {ModelRequestId} among {CandidateCount} candidates in catalog version {CatalogVersion}.")]
    internal static partial void NoCompatibleModel(
        ILogger logger,
        ModelRequestId modelRequestId,
        int candidateCount,
        long catalogVersion);

    /// <summary>Logs caller cancellation of model-catalog composition.</summary>
    [LoggerMessage(6103, LogLevel.Debug, "Model catalog refresh was cancelled.")]
    internal static partial void CatalogRefreshCancelled(ILogger logger);

    /// <summary>Logs a content-free model-catalog composition failure.</summary>
    [LoggerMessage(6104, LogLevel.Error, "Model catalog refresh failed with error type {ErrorType}.")]
    internal static partial void CatalogRefreshFailed(ILogger logger, string errorType);

    /// <summary>Logs caller cancellation of one model-selection operation.</summary>
    [LoggerMessage(6105, LogLevel.Debug, "Model selection for request {ModelRequestId} was cancelled.")]
    internal static partial void ModelSelectionCancelled(ILogger logger, ModelRequestId modelRequestId);

    /// <summary>Logs a content-free model-selection failure.</summary>
    [LoggerMessage(6106, LogLevel.Error, "Model selection for request {ModelRequestId} failed with error type {ErrorType}.")]
    internal static partial void ModelSelectionFailed(
        ILogger logger,
        ModelRequestId modelRequestId,
        string errorType);

    /// <summary>Logs that no adapter is registered for the selected alias.</summary>
    [LoggerMessage(6107, LogLevel.Error, "No LLM model adapter is registered for alias {ModelAlias}.")]
    internal static partial void ModelExecutionMissingAdapter(ILogger logger, ModelAlias modelAlias);

    /// <summary>Logs a successful model execution.</summary>
    [LoggerMessage(
        6108,
        LogLevel.Debug,
        "Model execution for request {ModelRequestId} completed on alias {ModelAlias} after {Attempts} attempt(s).")]
    internal static partial void ModelExecutionCompleted(
        ILogger logger,
        ModelRequestId modelRequestId,
        ModelAlias modelAlias,
        int attempts);

    /// <summary>Logs caller cancellation of one model execution.</summary>
    [LoggerMessage(
        6109,
        LogLevel.Debug,
        "Model execution for request {ModelRequestId} was cancelled after {Attempts} attempt(s).")]
    internal static partial void ModelExecutionCancelled(
        ILogger logger,
        ModelRequestId modelRequestId,
        int attempts);

    /// <summary>Logs a content-free model-execution failure.</summary>
    [LoggerMessage(
        6110,
        LogLevel.Error,
        "Model execution for request {ModelRequestId} failed with error type {ErrorType}.")]
    internal static partial void ModelExecutionFailed(
        ILogger logger,
        ModelRequestId modelRequestId,
        string errorType);

    /// <summary>Logs a terminal model execution failure.</summary>
    [LoggerMessage(
        6111,
        LogLevel.Warning,
        "Model execution for request {ModelRequestId} on alias {ModelAlias} failed with {FailureKind} after {Attempts} attempt(s).")]
    internal static partial void ModelExecutionTerminalFailure(
        ILogger logger,
        ModelRequestId modelRequestId,
        ModelAlias modelAlias,
        ProviderFailureKind failureKind,
        int attempts);

    /// <summary>Logs that same-model retries were exhausted and semantic fallback is required.</summary>
    [LoggerMessage(
        6112,
        LogLevel.Information,
        "Model execution for request {ModelRequestId} on alias {ModelAlias} requires fallback after {FailureKind} and {Attempts} attempt(s).")]
    internal static partial void ModelExecutionFallbackRequired(
        ILogger logger,
        ModelRequestId modelRequestId,
        ModelAlias modelAlias,
        ProviderFailureKind failureKind,
        int attempts);
}
