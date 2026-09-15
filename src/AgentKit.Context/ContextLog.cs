// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context;

/// <summary>Defines allocation-efficient structured context-preparation log events.</summary>
internal static partial class ContextLog
{
    /// <summary>Logs the safe structural input counts at context-preparation start.</summary>
    [LoggerMessage(2000, LogLevel.Debug, "Preparing context for model request {ModelRequestId} from {HistoryCount} history messages.")]
    internal static partial void Preparing(ILogger logger, ModelRequestId modelRequestId, int historyCount);

    /// <summary>Logs the safe structural output count after successful preparation.</summary>
    [LoggerMessage(2001, LogLevel.Debug, "Prepared context for model request {ModelRequestId} with {MessageCount} provider-ready messages.")]
    internal static partial void Prepared(ILogger logger, ModelRequestId modelRequestId, int messageCount);

    /// <summary>Logs the normalized reason for a rejected context preparation.</summary>
    [LoggerMessage(2002, LogLevel.Warning, "Rejected context preparation for model request {ModelRequestId} with reason {FailureKind}.")]
    internal static partial void Rejected(
        ILogger logger,
        ModelRequestId modelRequestId,
        ContextPreparationFailureKind failureKind);

    /// <summary>Logs that instruction-role messages found inside conversation history were excluded from the provider request.</summary>
    [LoggerMessage(2003, LogLevel.Warning, "Excluded {ExcludedCount} system/developer messages found in history for model request {ModelRequestId}; history never carries instruction authority.")]
    internal static partial void ExcludedInstructionMessagesFromHistory(ILogger logger, ModelRequestId modelRequestId, int excludedCount);
}
