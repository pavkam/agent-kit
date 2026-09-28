// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

/// <summary>Defines stable content-free structured log events owned by the durability runtime.</summary>
/// <remarks>
/// Every template carries bounded stage, outcome, and registration-name values only. Operation payloads, prompts,
/// results, credentials, and domain identities other than registration names are never logged here.
/// </remarks>
internal static partial class DurabilityLog
{
    [LoggerMessage(EventId = 26000, Level = LogLevel.Information, Message = "Durable coordinator stage {Stage} completed with {Outcome}.")]
    internal static partial void StageCompleted(ILogger logger, string stage, string outcome);

    [LoggerMessage(EventId = 26001, Level = LogLevel.Debug, Message = "Durable coordinator stage {Stage} was cancelled.")]
    internal static partial void StageCancelled(ILogger logger, string stage);

    [LoggerMessage(EventId = 26002, Level = LogLevel.Error, Message = "Durable coordinator stage {Stage} failed with {ErrorType}.")]
    internal static partial void StageFailed(ILogger logger, string stage, string errorType);

    [LoggerMessage(EventId = 26003, Level = LogLevel.Warning, Message = "Durable coordinator stage {Stage} could not proceed because a required component reported {Outcome}.")]
    internal static partial void StageUnavailable(ILogger logger, string stage, string outcome);

    [LoggerMessage(EventId = 26004, Level = LogLevel.Warning, Message = "Durable execution lease was lost before stage {Stage} could write.")]
    internal static partial void LeaseLost(ILogger logger, string stage);

    [LoggerMessage(EventId = 26005, Level = LogLevel.Debug, Message = "Durable coordinator renewed its execution lease for stage {Stage}.")]
    internal static partial void LeaseRenewed(ILogger logger, string stage);

    [LoggerMessage(EventId = 26006, Level = LogLevel.Warning, Message = "Observational durable execution event sink {SinkId} is not registered and was skipped.")]
    internal static partial void EventSinkSkipped(ILogger logger, string sinkId);

    [LoggerMessage(EventId = 26007, Level = LogLevel.Warning, Message = "Durable execution event sink {SinkId} failed with {ErrorType}.")]
    internal static partial void EventSinkFailed(ILogger logger, string sinkId, string errorType);

    [LoggerMessage(EventId = 26008, Level = LogLevel.Error, Message = "Required durable execution event sink {SinkId} is not available.")]
    internal static partial void RequiredEventSinkUnavailable(ILogger logger, string sinkId);

    [LoggerMessage(EventId = 26009, Level = LogLevel.Warning, Message = "Recovery for a durable operation requires operator action after evidence produced {Outcome}.")]
    internal static partial void OperatorActionRequired(ILogger logger, string outcome);
}
