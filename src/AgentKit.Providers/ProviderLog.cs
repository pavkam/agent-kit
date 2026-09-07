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
}
