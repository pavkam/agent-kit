// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json;

/// <summary>Defines content-free source-generated events for the JSON security-decision-store adapter.</summary>
internal static partial class JsonSecurityDecisionStoreLog
{
    /// <summary>Logs successful terminal store observation.</summary>
    /// <param name="logger">The content-free logger.</param>
    /// <param name="operation">The bounded operation name.</param>
    /// <param name="outcome">The bounded terminal outcome.</param>
    /// <param name="securityRequestId">The security request identity when established.</param>
    [LoggerMessage(19120, LogLevel.Debug, "JSON security decision store operation {Operation} completed with outcome {Outcome}; request {SecurityRequestId}.")]
    internal static partial void OperationCompleted(ILogger logger, string operation, string outcome,
        string? securityRequestId);

    /// <summary>Logs a non-successful terminal store observation without persisted or protected content.</summary>
    /// <param name="logger">The content-free logger.</param>
    /// <param name="operation">The bounded operation name.</param>
    /// <param name="outcome">The bounded terminal outcome.</param>
    /// <param name="failureKind">The normalized bounded failure class.</param>
    /// <param name="securityRequestId">The security request identity when established.</param>
    [LoggerMessage(19121, LogLevel.Warning, "JSON security decision store operation {Operation} completed with outcome {Outcome} and failure kind {FailureKind}; request {SecurityRequestId}.")]
    internal static partial void OperationFailed(ILogger logger, string operation, string outcome, string failureKind,
        string? securityRequestId);
}
