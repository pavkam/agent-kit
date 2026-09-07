// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output;

/// <summary>Defines allocation-efficient output-processing events without candidate or repair content.</summary>
internal static partial class OutputLog
{
    /// <summary>Records a terminal output decision without candidate, schema, issue, or repair content.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="outputDefinitionId">The immutable output contract identity.</param>
    /// <param name="outputMode">The normalized output protocol mode.</param>
    /// <param name="validationAttempt">The one-based validation attempt.</param>
    /// <param name="outcome">The normalized processing decision.</param>
    [LoggerMessage(10000, LogLevel.Debug, "Output definition {OutputDefinitionId} mode {OutputMode} attempt {ValidationAttempt} completed with outcome {Outcome}.")]
    internal static partial void Completed(
        ILogger logger,
        OutputDefinitionId outputDefinitionId,
        OutputMode outputMode,
        int validationAttempt,
        string outcome);

    /// <summary>Records caller cancellation of output processing before a decision was returned.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="outputDefinitionId">The immutable output contract identity.</param>
    /// <param name="outputMode">The normalized output protocol mode.</param>
    /// <param name="validationAttempt">The one-based validation attempt.</param>
    [LoggerMessage(10001, LogLevel.Debug, "Output definition {OutputDefinitionId} mode {OutputMode} attempt {ValidationAttempt} was cancelled.")]
    internal static partial void Cancelled(
        ILogger logger, OutputDefinitionId outputDefinitionId, OutputMode outputMode, int validationAttempt);

    /// <summary>Records an unexpected output-processing exception without candidate or validation content.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="outputDefinitionId">The immutable output contract identity.</param>
    /// <param name="outputMode">The normalized output protocol mode.</param>
    /// <param name="validationAttempt">The one-based validation attempt.</param>
    /// <param name="errorType">The exception type raised by output processing.</param>
    [LoggerMessage(10002, LogLevel.Error, "Output definition {OutputDefinitionId} mode {OutputMode} attempt {ValidationAttempt} failed with error type {ErrorType}.")]
    internal static partial void Failed(
        ILogger logger,
        OutputDefinitionId outputDefinitionId,
        OutputMode outputMode,
        int validationAttempt,
        string errorType);

    /// <summary>Records immutable output-registry composition using bounded definition and version counts.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="definitionCount">The number of distinct definition identities.</param>
    /// <param name="versionCount">The total number of registered definition versions.</param>
    [LoggerMessage(10010, LogLevel.Information, "Composed output definition registry with {DefinitionCount} definitions and {VersionCount} versions.")]
    internal static partial void RegistryComposed(ILogger logger, int definitionCount, int versionCount);

    /// <summary>Records one output-definition lookup without names, schema, runtime types, or validation content.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="outputDefinitionId">The requested immutable definition identity.</param>
    /// <param name="outputDefinitionVersion">The requested or selected version text.</param>
    /// <param name="outcome">The bounded lookup outcome.</param>
    [LoggerMessage(10011, LogLevel.Debug, "Output definition {OutputDefinitionId} version {OutputDefinitionVersion} lookup completed with outcome {Outcome}.")]
    internal static partial void DefinitionResolved(
        ILogger logger,
        OutputDefinitionId outputDefinitionId,
        string? outputDefinitionVersion,
        string outcome);
}
