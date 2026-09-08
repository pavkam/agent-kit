// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.InMemory;

/// <summary>Defines allocation-efficient, content-free grant-store diagnostic events.</summary>
internal static partial class InMemorySecurityGrantStoreLog
{
    /// <summary>Logs atomic enforcement-intent consumption without protected resource or input content.</summary>
    /// <param name="logger">The content-free logger that receives the structured event.</param>
    /// <param name="securityRequestId">The request whose grant use reached a bounded terminal disposition.</param>
    /// <param name="outcome">The bounded grant-consumption status name.</param>
    /// <remarks>The event excludes grant resources, effect fingerprints, identity claims, and caller content.</remarks>
    [LoggerMessage(5025, LogLevel.Debug, "Security grant for request {SecurityRequestId} completed intent consumption with outcome {Outcome}.")]
    internal static partial void GrantConsumptionCompleted(
        ILogger logger,
        SecurityRequestId securityRequestId,
        string outcome);

    /// <summary>Logs caller cancellation before atomic intent consumption commits.</summary>
    /// <param name="logger">The content-free logger that receives the structured event.</param>
    /// <param name="securityRequestId">The request whose pending grant consumption the caller cancelled.</param>
    /// <remarks>The event makes no claim that a separately reconciled external effect completed.</remarks>
    [LoggerMessage(5026, LogLevel.Information, "Security grant consumption for request {SecurityRequestId} was cancelled.")]
    internal static partial void GrantConsumptionCancelled(ILogger logger, SecurityRequestId securityRequestId);

    /// <summary>Logs an unexpected pre-consumption failure without protected input or exception-message content.</summary>
    /// <param name="logger">The content-free logger that receives the structured event.</param>
    /// <param name="securityRequestId">The request whose grant consumption faulted.</param>
    /// <param name="errorType">The exception type name, excluding its message and protected values.</param>
    /// <remarks>The event is observational and does not convert or replace the original exception.</remarks>
    [LoggerMessage(5027, LogLevel.Error, "Security grant consumption for request {SecurityRequestId} faulted with error type {ErrorType}.")]
    internal static partial void GrantConsumptionFaulted(
        ILogger logger,
        SecurityRequestId securityRequestId,
        string errorType);
}
