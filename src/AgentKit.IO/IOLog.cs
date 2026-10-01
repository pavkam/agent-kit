// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Defines stable content-free structured input/output log events.</summary>
internal static partial class IOLog
{
    [LoggerMessage(EventId = 22000, Level = LogLevel.Information, Message = "Input promotion planning completed at {Boundary} with {Outcome}.")]
    internal static partial void PromotionPlanCompleted(ILogger logger, PromotionBoundary boundary, string outcome);

    [LoggerMessage(EventId = 22001, Level = LogLevel.Information, Message = "Input promotion planning was cancelled at {Boundary}.")]
    internal static partial void PromotionPlanCancelled(ILogger logger, PromotionBoundary boundary);

    [LoggerMessage(EventId = 22002, Level = LogLevel.Error, Message = "Input promotion planning failed at {Boundary} with {ErrorType}.")]
    internal static partial void PromotionPlanFailed(ILogger logger, PromotionBoundary boundary, string errorType);

    [LoggerMessage(EventId = 22003, Level = LogLevel.Information, Message = "Run final result was exposed with {Outcome}.")]
    internal static partial void FinalResultPublished(ILogger logger, string outcome);

    [LoggerMessage(EventId = 22004, Level = LogLevel.Warning, Message = "A conflicting run final result was rejected; the original envelope is retained.")]
    internal static partial void FinalResultConflicted(ILogger logger);

    [LoggerMessage(EventId = 22005, Level = LogLevel.Information, Message = "Input admission completed with {Outcome}.")]
    internal static partial void InputAdmissionCompleted(ILogger logger, string outcome);

    [LoggerMessage(EventId = 22006, Level = LogLevel.Error, Message = "Input admission failed with {ErrorType}.")]
    internal static partial void InputAdmissionFailed(ILogger logger, string errorType);

    [LoggerMessage(EventId = 22007, Level = LogLevel.Information, Message = "Input promotion completed at {Boundary} with {Outcome}.")]
    internal static partial void InputPromotionCompleted(ILogger logger, PromotionBoundary boundary, string outcome);

    [LoggerMessage(EventId = 22008, Level = LogLevel.Error, Message = "Input promotion failed at {Boundary} with {ErrorType}.")]
    internal static partial void InputPromotionFailed(ILogger logger, PromotionBoundary boundary, string errorType);

    [LoggerMessage(EventId = 22009, Level = LogLevel.Error, Message = "Required run-event sink {SinkName} faulted with {ErrorType}; the run observes this failure.")]
    internal static partial void RequiredRunEventSinkFaulted(ILogger logger, string sinkName, string errorType);

    [LoggerMessage(EventId = 22010, Level = LogLevel.Warning, Message = "Best-effort run-event sink {SinkName} faulted with {ErrorType}; the run continues without it.")]
    internal static partial void BestEffortRunEventSinkFaulted(ILogger logger, string sinkName, string errorType);

    [LoggerMessage(EventId = 22011, Level = LogLevel.Warning, Message = "Best-effort run-event sink {SinkName} delivery was {Decision} after remaining blocked.")]
    internal static partial void BestEffortRunEventSinkBackpressured(ILogger logger, string sinkName, BackpressureDecision decision);

    [LoggerMessage(EventId = 22012, Level = LogLevel.Error, Message = "The backpressure policy returned {Decision} for required sink {SinkName}; only Wait is valid for a required sink.")]
    internal static partial void RequiredRunEventSinkBackpressureMisconfigured(ILogger logger, string sinkName, BackpressureDecision decision);

    [LoggerMessage(EventId = 22013, Level = LogLevel.Debug, Message = "Required run-event sink {SinkName} drained during shutdown.")]
    internal static partial void RequiredRunEventSinkDrained(ILogger logger, string sinkName);

    [LoggerMessage(EventId = 22014, Level = LogLevel.Warning, Message = "Required run-event sink {SinkName} did not drain within its flush deadline of {FlushDeadline}; accepted events may be undelivered.")]
    internal static partial void RequiredRunEventSinkFlushTimedOut(ILogger logger, string sinkName, TimeSpan flushDeadline);

    [LoggerMessage(EventId = 22015, Level = LogLevel.Error, Message = "Required run-event sink {SinkName} faulted while draining with {ErrorType}; accepted events may be undelivered.")]
    internal static partial void RequiredRunEventSinkFlushFaulted(ILogger logger, string sinkName, string errorType);
}
