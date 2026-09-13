// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory;

/// <summary>Defines stable content-free structured execution-lease log events.</summary>
internal static partial class DurableLeaseLog
{
    [LoggerMessage(EventId = 20000, Level = LogLevel.Information, Message = "Execution lease acquisition for worker {WorkerId} completed with {Outcome}.")]
    internal static partial void AcquisitionCompleted(ILogger logger, WorkerId workerId, string outcome);

    [LoggerMessage(EventId = 20001, Level = LogLevel.Debug, Message = "Execution lease acquisition for worker {WorkerId} was cancelled.")]
    internal static partial void AcquisitionCancelled(ILogger logger, WorkerId workerId);

    [LoggerMessage(EventId = 20002, Level = LogLevel.Error, Message = "Execution lease acquisition for worker {WorkerId} failed with {ErrorType}.")]
    internal static partial void AcquisitionFailed(ILogger logger, WorkerId workerId, string errorType);

    [LoggerMessage(EventId = 20003, Level = LogLevel.Information, Message = "Execution lease renewal for worker {WorkerId} completed with {Outcome}.")]
    internal static partial void RenewalCompleted(ILogger logger, WorkerId workerId, string outcome);

    [LoggerMessage(EventId = 20004, Level = LogLevel.Debug, Message = "Execution lease renewal for worker {WorkerId} was cancelled.")]
    internal static partial void RenewalCancelled(ILogger logger, WorkerId workerId);

    [LoggerMessage(EventId = 20005, Level = LogLevel.Error, Message = "Execution lease renewal for worker {WorkerId} failed with {ErrorType}.")]
    internal static partial void RenewalFailed(ILogger logger, WorkerId workerId, string errorType);

    [LoggerMessage(EventId = 20006, Level = LogLevel.Debug, Message = "Execution lease for worker {WorkerId} was released.")]
    internal static partial void Released(ILogger logger, WorkerId workerId);
}
