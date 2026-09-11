// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Defines allocation-efficient structured tool-lifecycle log events.</summary>
internal static partial class ToolLog
{
    /// <summary>Records source discovery before capture ownership transfers.</summary>
    /// <param name="logger">The provider-specific logger.</param><param name="sourceId">The stable selected source.</param><param name="tenantId">The request tenant.</param><param name="principalId">The authenticated principal.</param><param name="agentId">The selected agent.</param><param name="sessionId">The owning session.</param><param name="runId">The active run.</param>
    [LoggerMessage(4050, LogLevel.Debug, "Discovering tool source {SourceId} for tenant {TenantId}, principal {PrincipalId}, agent {AgentId}, session {SessionId}, run {RunId}.")]
    internal static partial void ProviderDiscoveryStarted(ILogger logger, ToolSourceId sourceId, TenantId tenantId, PrincipalId principalId, AgentId agentId, SessionId sessionId, RunId runId);

    /// <summary>Records the discovery outcome without tool descriptors or request content.</summary>
    /// <param name="logger">The provider-specific logger.</param><param name="level">The outcome-derived severity.</param><param name="sourceId">The selected source.</param><param name="sourceVersion">The retained publication version.</param><param name="tenantId">The request tenant.</param><param name="principalId">The authenticated principal.</param><param name="agentId">The selected agent.</param><param name="sessionId">The owning session.</param><param name="runId">The active run.</param><param name="outcome">The bounded terminal outcome.</param>
    [LoggerMessage(EventId = 4051, Message = "Tool source {SourceId} at version {SourceVersion} discovery {Outcome} for tenant {TenantId}, principal {PrincipalId}, agent {AgentId}, session {SessionId}, run {RunId}.")]
    internal static partial void ProviderDiscoveryCompleted(ILogger logger, LogLevel level, ToolSourceId sourceId, ToolSourceVersion sourceVersion, TenantId tenantId, PrincipalId principalId, AgentId agentId, SessionId sessionId, RunId runId, string outcome);

    /// <summary>Records a catalog acquisition or lifetime stage before source callbacks.</summary>
    /// <param name="logger">The capture-specific logger.</param><param name="operation">The bounded stage name.</param><param name="tenantId">The captured tenant.</param><param name="principalId">The authenticated principal.</param><param name="agentId">The catalog agent.</param><param name="sessionId">The owning session.</param><param name="runId">The captured active run.</param><param name="catalogVersion">The exact retained catalog version.</param>
    [LoggerMessage(4040, LogLevel.Debug, "Starting {Operation} for tenant {TenantId}, principal {PrincipalId}, agent {AgentId}, session {SessionId}, run {RunId} and tool catalog {CatalogVersion}.")]
    internal static partial void CatalogCaptureStarted(ILogger logger, string operation, TenantId tenantId, PrincipalId principalId, AgentId agentId, SessionId sessionId, RunId runId, ToolCatalogVersion catalogVersion);

    /// <summary>Records a terminal catalog stage without arbitrary source reasons or exception content.</summary>
    /// <param name="logger">The capture-specific logger.</param><param name="level">The severity selected from the bounded semantic outcome.</param><param name="operation">The bounded stage name.</param><param name="outcome">The bounded terminal outcome.</param><param name="tenantId">The captured tenant.</param><param name="principalId">The authenticated principal.</param><param name="agentId">The catalog agent.</param><param name="sessionId">The owning session.</param><param name="runId">The captured active run.</param><param name="catalogVersion">The exact catalog version.</param><param name="errorType">Only the caught CLR type name, or null when no exception occurred.</param>
    [LoggerMessage(EventId = 4041, Message = "Completed {Operation} with {Outcome} for tenant {TenantId}, principal {PrincipalId}, agent {AgentId}, session {SessionId}, run {RunId} and tool catalog {CatalogVersion}; error type {ErrorType}.")]
    internal static partial void CatalogCaptureCompleted(ILogger logger, LogLevel level, string operation, string outcome, TenantId tenantId, PrincipalId principalId, AgentId agentId, SessionId sessionId, RunId runId, ToolCatalogVersion catalogVersion, string? errorType);

    /// <summary>Records a retained source-capture operation before observable work.</summary>
    /// <param name="logger">The capture's type-specific logger.</param><param name="operation">The bounded operation name.</param><param name="sourceId">The captured source identity.</param><param name="sourceVersion">The exact source publication.</param>
    [LoggerMessage(4030, LogLevel.Debug, "Starting {Operation} for tool source {SourceId} at version {SourceVersion}.")]
    internal static partial void CaptureOperationStarted(ILogger logger, string operation, ToolSourceId sourceId, ToolSourceVersion sourceVersion);

    /// <summary>Records successful completion without descriptor or result content.</summary>
    /// <param name="logger">The capture's type-specific logger.</param><param name="operation">The bounded operation name.</param><param name="outcome">The bounded successful outcome.</param><param name="sourceId">The captured source identity.</param><param name="sourceVersion">The exact source publication.</param>
    [LoggerMessage(4031, LogLevel.Debug, "Completed {Operation} with {Outcome} for tool source {SourceId} at version {SourceVersion}.")]
    internal static partial void CaptureOperationCompleted(ILogger logger, string operation, string outcome, ToolSourceId sourceId, ToolSourceVersion sourceVersion);

    /// <summary>Records acquisition cancellation without claiming an unavailable publication.</summary>
    /// <param name="logger">The capture's type-specific logger.</param><param name="sourceId">The captured source identity.</param><param name="sourceVersion">The exact source publication.</param>
    [LoggerMessage(4032, LogLevel.Information, "Cancelled invoker acquisition for tool source {SourceId} at version {SourceVersion}.")]
    internal static partial void CaptureAcquisitionCancelled(ILogger logger, ToolSourceId sourceId, ToolSourceVersion sourceVersion);

    /// <summary>Records owned cleanup failure without copying its exception message or other protected content.</summary>
    /// <param name="logger">The capture's type-specific logger.</param><param name="operation">The bounded operation name.</param><param name="sourceId">The captured source identity.</param><param name="sourceVersion">The exact source publication.</param><param name="errorType">The exception type name, without message or stack trace.</param>
    [LoggerMessage(4033, LogLevel.Error, "Failed {Operation} for tool source {SourceId} at version {SourceVersion} with error type {ErrorType}.")]
    internal static partial void CaptureOperationFailed(ILogger logger, string operation, ToolSourceId sourceId, ToolSourceVersion sourceVersion, string errorType);

    /// <summary>Records unavailable exact acquisition without exposing alternative bindings or arbitrary reason content.</summary>
    /// <param name="logger">The capture's type-specific logger.</param><param name="sourceId">The captured source identity.</param><param name="sourceVersion">The exact source publication.</param><param name="toolId">The requested canonical identity.</param><param name="toolVersion">The requested exact tool version.</param>
    [LoggerMessage(4034, LogLevel.Warning, "Invoker {ToolId} at version {ToolVersion} is unavailable from tool source {SourceId} at version {SourceVersion}.")]
    internal static partial void CaptureInvokerUnavailable(ILogger logger, ToolSourceId sourceId, ToolSourceVersion sourceVersion, ToolId toolId, ToolVersion toolVersion);

    /// <summary>Records lookup start using only the captured policy reference.</summary>
    /// <param name="logger">The catalog's type-specific logger.</param><param name="policyKey">The requested policy key.</param><param name="policyVersion">The exact requested revision.</param>
    [LoggerMessage(4020, LogLevel.Debug, "Resolving tool-result projection policy {PolicyKey} at version {PolicyVersion}.")]
    internal static partial void ProjectionPolicyResolutionStarted(ILogger logger, ToolResultProjectionPolicyKey policyKey, ToolResultProjectionPolicyVersion policyVersion);

    /// <summary>Records successful exact lookup without copying policy content.</summary>
    /// <param name="logger">The catalog's type-specific logger.</param><param name="policyKey">The resolved policy key.</param><param name="policyVersion">The exact resolved revision.</param>
    [LoggerMessage(4021, LogLevel.Debug, "Resolved tool-result projection policy {PolicyKey} at version {PolicyVersion}.")]
    internal static partial void ProjectionPolicyResolved(ILogger logger, ToolResultProjectionPolicyKey policyKey, ToolResultProjectionPolicyVersion policyVersion);

    /// <summary>Records unavailable retained policy content without listing alternative policies.</summary>
    /// <param name="logger">The catalog's type-specific logger.</param><param name="policyKey">The unavailable policy key.</param><param name="policyVersion">The exact unavailable revision.</param>
    [LoggerMessage(4022, LogLevel.Warning, "Tool-result projection policy {PolicyKey} at version {PolicyVersion} is unavailable.")]
    internal static partial void ProjectionPolicyUnavailable(ILogger logger, ToolResultProjectionPolicyKey policyKey, ToolResultProjectionPolicyVersion policyVersion);

    /// <summary>Records caller cancellation without converting it to an unavailable policy.</summary>
    /// <param name="logger">The catalog's type-specific logger.</param><param name="policyKey">The requested policy key.</param><param name="policyVersion">The exact requested revision.</param>
    [LoggerMessage(4023, LogLevel.Information, "Cancelled tool-result projection-policy resolution for {PolicyKey} at version {PolicyVersion}.")]
    internal static partial void ProjectionPolicyResolutionCancelled(ILogger logger, ToolResultProjectionPolicyKey policyKey, ToolResultProjectionPolicyVersion policyVersion);

    /// <summary>Logs the start of one identified tool call without its arguments.</summary>
    [LoggerMessage(4000, LogLevel.Debug, "Starting tool call {ToolCallId} for tool {ToolId}.")]
    internal static partial void Started(ILogger logger, ToolCallId toolCallId, ToolId toolId);

    /// <summary>Logs rejection because the requested tool is absent.</summary>
    [LoggerMessage(4001, LogLevel.Warning, "Rejected tool call {ToolCallId} because tool {ToolId} is not registered.")]
    internal static partial void Unknown(ILogger logger, ToolCallId toolCallId, ToolId toolId);

    /// <summary>Logs rejection by the configured authorization policy.</summary>
    [LoggerMessage(4002, LogLevel.Warning, "Denied tool call {ToolCallId} for tool {ToolId}.")]
    internal static partial void Denied(ILogger logger, ToolCallId toolCallId, ToolId toolId);

    /// <summary>Logs the normalized terminal result of an invoked tool.</summary>
    [LoggerMessage(4003, LogLevel.Debug, "Completed tool call {ToolCallId} for tool {ToolId} with outcome {Outcome}.")]
    internal static partial void Completed(
        ILogger logger,
        ToolCallId toolCallId,
        ToolId toolId,
        ToolCallOutcomeKind outcome);

    /// <summary>Logs caller cancellation without copying tool arguments or results.</summary>
    [LoggerMessage(4004, LogLevel.Information, "Cancelled tool call {ToolCallId} for tool {ToolId}.")]
    internal static partial void Cancelled(ILogger logger, ToolCallId toolCallId, ToolId toolId);

    /// <summary>Logs an unexpected implementation exception type without copying its potentially sensitive message.</summary>
    [LoggerMessage(4005, LogLevel.Error, "Tool call {ToolCallId} for tool {ToolId} failed with error type {ErrorType}.")]
    internal static partial void Failed(ILogger logger, ToolCallId toolCallId, ToolId toolId, string errorType);
}
