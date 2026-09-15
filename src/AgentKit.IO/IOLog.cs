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
}
