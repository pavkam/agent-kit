// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

/// <summary>Defines content-free source-generated events for the durable JSON session-directory adapter.</summary>
/// <remarks>
/// Event identities are owned by this package and stable across releases. Only bounded operation names, typed domain
/// identities, and a fixed outcome vocabulary are emitted; store routes, tenant text, and the configured root path are
/// never logged.
/// </remarks>
internal static partial class JsonSessionDirectoryLog
{
    /// <summary>Records one typed terminal directory outcome using causal domain identities.</summary>
    /// <param name="logger">The configured Microsoft logger.</param>
    /// <param name="operation">The bounded directory operation name.</param>
    /// <param name="agentId">The owning agent identity.</param>
    /// <param name="sessionId">The session identity when already established.</param>
    /// <param name="outcome">The bounded terminal success, rejection, or cancellation classification.</param>
    [LoggerMessage(19310, LogLevel.Debug, "JSON session directory operation {Operation} for agent {AgentId} and session {SessionId} completed with outcome {Outcome}.")]
    internal static partial void Completed(
        ILogger logger, string operation, AgentId agentId, SessionId? sessionId, string outcome);

    /// <summary>Records an unexpected directory failure type without exception, route, or path content.</summary>
    /// <param name="logger">The configured Microsoft logger.</param>
    /// <param name="operation">The bounded directory operation name.</param>
    /// <param name="agentId">The owning agent identity.</param>
    /// <param name="sessionId">The session identity when already established.</param>
    /// <param name="errorType">The stable exception type name.</param>
    [LoggerMessage(19311, LogLevel.Error, "JSON session directory operation {Operation} for agent {AgentId} and session {SessionId} failed with error type {ErrorType}.")]
    internal static partial void Failed(
        ILogger logger, string operation, AgentId agentId, SessionId? sessionId, string errorType);
}
