// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Defines stable content-free structured input/output log events.</summary>
internal static partial class IOLog
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "Input promotion planning completed at {Boundary} with {Outcome}.")]
    internal static partial void PromotionPlanCompleted(ILogger logger, PromotionBoundary boundary, string outcome);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Input promotion planning was cancelled at {Boundary}.")]
    internal static partial void PromotionPlanCancelled(ILogger logger, PromotionBoundary boundary);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Error, Message = "Input promotion planning failed at {Boundary} with {ErrorType}.")]
    internal static partial void PromotionPlanFailed(ILogger logger, PromotionBoundary boundary, string errorType);
}
