// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Defines allocation-efficient structured tool-lifecycle log events.</summary>
internal static partial class ToolLog
{
    /// <summary>Records entry to one recorder write using safe correlation identities only.</summary>
    /// <param name="logger">The recorder's type-specific logger.</param><param name="stage">The bounded record stage: accepted or terminal.</param><param name="agentId">The owning agent.</param><param name="sessionId">The owning session.</param><param name="runId">The active run.</param><param name="turnId">The active turn.</param><param name="toolCallId">The correlated call.</param>
    [LoggerMessage(4110, LogLevel.Debug, "Recording {Stage} tool call {ToolCallId} for agent {AgentId}, session {SessionId}, run {RunId}, turn {TurnId}.")]
    internal static partial void CallRecordStarted(ILogger logger, string stage, AgentId agentId, SessionId sessionId, RunId runId, TurnId turnId, ToolCallId toolCallId);

    /// <summary>Records the bounded terminal outcome of one recorder write without arguments, results, or exception text.</summary>
    /// <param name="logger">The recorder's type-specific logger.</param><param name="level">Severity matching the semantic outcome.</param><param name="stage">The bounded record stage: accepted or terminal.</param><param name="agentId">The owning agent.</param><param name="sessionId">The owning session.</param><param name="runId">The active run.</param><param name="turnId">The active turn.</param><param name="toolCallId">The correlated call.</param><param name="outcome">A closed recording outcome.</param>
    [LoggerMessage(EventId = 4111, Message = "Recorded {Stage} tool call {ToolCallId} for agent {AgentId}, session {SessionId}, run {RunId}, turn {TurnId} with outcome {Outcome}.")]
    internal static partial void CallRecordCompleted(ILogger logger, LogLevel level, string stage, AgentId agentId, SessionId sessionId, RunId runId, TurnId turnId, ToolCallId toolCallId, string outcome);

    /// <summary>Records that no execution policy is registered under the exact captured reference.</summary>
    /// <param name="logger">The selector's type-specific logger.</param><param name="policyKey">The unavailable policy key.</param><param name="policyVersion">The exact unavailable revision.</param>
    [LoggerMessage(4120, LogLevel.Warning, "Tool execution policy {PolicyKey} at version {PolicyVersion} is unavailable.")]
    internal static partial void ExecutionPolicyUnavailable(ILogger logger, ToolExecutionPolicyKey policyKey, ToolExecutionPolicyVersion policyVersion);

    /// <summary>Records exact selection of an execution policy without copying its plan.</summary>
    /// <param name="logger">The selector's type-specific logger.</param><param name="policyKey">The selected policy key.</param><param name="policyVersion">The exact selected revision.</param>
    [LoggerMessage(4121, LogLevel.Debug, "Selected tool execution policy {PolicyKey} at version {PolicyVersion}.")]
    internal static partial void ExecutionPolicySelected(ILogger logger, ToolExecutionPolicyKey policyKey, ToolExecutionPolicyVersion policyVersion);

    /// <summary>Records that a selected policy refused or could not produce a valid plan.</summary>
    /// <param name="logger">The executor's type-specific logger.</param><param name="policyKey">The planning policy key.</param><param name="policyVersion">The planning policy revision.</param><param name="reason">A closed bounded reason: rejected, mismatched, or faulted.</param>
    [LoggerMessage(4122, LogLevel.Warning, "Tool execution policy {PolicyKey} at version {PolicyVersion} did not plan its calls: {Reason}.")]
    internal static partial void ExecutionPlanRefused(ILogger logger, ToolExecutionPolicyKey policyKey, ToolExecutionPolicyVersion policyVersion, string reason);

    /// <summary>Records that one sink failed or timed out while observing an event, without the event content.</summary>
    /// <param name="logger">The dispatcher's type-specific logger.</param><param name="sinkId">The registered sink identity.</param><param name="errorType">The exception type name, or Timeout.</param>
    [LoggerMessage(4130, LogLevel.Warning, "Tool event sink {SinkId} failed to observe an event with error type {ErrorType}.")]
    internal static partial void EventSinkFailed(ILogger logger, string sinkId, string errorType);

    /// <summary>Records a scheduled retry of a failed tool attempt without arguments, results, or failure text.</summary>
    /// <param name="logger">The scheduler's type-specific logger.</param><param name="toolCallId">The correlated call.</param><param name="toolId">The retried tool.</param><param name="failedAttempt">The positive attempt that failed.</param><param name="delay">The backoff before the next attempt.</param>
    [LoggerMessage(4140, LogLevel.Information, "Retrying tool call {ToolCallId} for tool {ToolId} after failed attempt {FailedAttempt} with backoff {Delay}.")]
    internal static partial void RetryScheduled(ILogger logger, ToolCallId toolCallId, ToolId toolId, int failedAttempt, TimeSpan delay);

    /// <summary>Records the bounded outcome of externalizing one oversized tool result, without result content or artifact locations.</summary>
    /// <param name="logger">The spill's type-specific logger.</param><param name="level">Severity matching the semantic outcome.</param><param name="toolCallId">The correlated call.</param><param name="toolId">The tool whose result was oversized.</param><param name="outcome">A closed outcome: spilled, refused, timed_out, or faulted.</param>
    [LoggerMessage(EventId = 4141, Message = "Tool result spill for call {ToolCallId} of tool {ToolId} ended with {Outcome}.")]
    internal static partial void ResultSpillCompleted(ILogger logger, LogLevel level, ToolCallId toolCallId, ToolId toolId, string outcome);

    /// <summary>Records that one invocation attempt reached its enforced deadline, without arguments, results, or exception text.</summary>
    /// <param name="logger">The scheduler's type-specific logger.</param><param name="toolCallId">The correlated call.</param><param name="toolId">The tool that timed out.</param><param name="attempt">The positive attempt that timed out.</param><param name="abandoned">Whether the attempt ignored cancellation through the drain period and was abandoned.</param>
    [LoggerMessage(4142, LogLevel.Warning, "Tool call {ToolCallId} for tool {ToolId} attempt {Attempt} reached its invocation deadline; abandoned {Abandoned}.")]
    internal static partial void InvocationTimedOut(ILogger logger, ToolCallId toolCallId, ToolId toolId, int attempt, bool abandoned);

    /// <summary>Records that a tool budget reservation was refused or unavailable and what the executor did about it.</summary>
    /// <param name="logger">The scheduler's type-specific logger.</param><param name="toolCallId">The correlated call.</param><param name="toolId">The tool whose call was affected.</param><param name="dimension">The first-party tool budget dimension.</param><param name="outcome">A closed outcome: exhausted or unavailable.</param>
    [LoggerMessage(4143, LogLevel.Warning, "Tool budget dimension {Dimension} for call {ToolCallId} of tool {ToolId} was {Outcome}.")]
    internal static partial void BudgetReservationDeclined(ILogger logger, string dimension, ToolCallId toolCallId, ToolId toolId, string outcome);

    /// <summary>Records entry to coordinated discovery, merge, and schema/capability preflight without publication content.</summary>
    /// <param name="logger">The coordinator's type-specific logger.</param><param name="tenantId">The captured tenant.</param><param name="principalId">The captured principal.</param><param name="agentId">The selected agent.</param><param name="sessionId">The selected session.</param><param name="runId">The active run.</param>
    [LoggerMessage(4100, LogLevel.Debug, "Starting tool catalog coordination for tenant {TenantId}, principal {PrincipalId}, agent {AgentId}, session {SessionId}, run {RunId}.")]
    internal static partial void CatalogCoordinationStarted(ILogger logger, TenantId tenantId, PrincipalId principalId, AgentId agentId, SessionId sessionId, RunId runId);

    /// <summary>Records a bounded terminal coordination outcome without publication, schema, or exception content.</summary>
    /// <param name="logger">The coordinator's type-specific logger.</param><param name="level">Severity matching the semantic outcome.</param><param name="tenantId">The captured tenant.</param><param name="principalId">The captured principal.</param><param name="agentId">The selected agent.</param><param name="sessionId">The selected session.</param><param name="runId">The active run.</param><param name="outcome">A closed coordination result.</param>
    [LoggerMessage(EventId = 4101, Message = "Completed tool catalog coordination for tenant {TenantId}, principal {PrincipalId}, agent {AgentId}, session {SessionId}, run {RunId} with outcome {Outcome}.")]
    internal static partial void CatalogCoordinationCompleted(ILogger logger, LogLevel level, TenantId tenantId, PrincipalId principalId, AgentId agentId, SessionId sessionId, RunId runId, string outcome);

    /// <summary>Records entry to canonical schema compilation or validation without content.</summary>
    /// <param name="logger">The emitting-type logger.</param><param name="operation">The bounded schema stage.</param>
    [LoggerMessage(4090, LogLevel.Debug, "Starting tool schema operation {Operation}.")]
    internal static partial void SchemaStarted(ILogger logger, string operation);

    /// <summary>Records a terminal schema outcome without metadata, instance, or exception content.</summary>
    /// <param name="logger">The emitting-type logger.</param><param name="level">The semantic outcome severity.</param><param name="operation">The bounded schema stage.</param><param name="outcome">The bounded terminal outcome.</param>
    [LoggerMessage(EventId = 4091, Message = "Completed tool schema operation {Operation} with outcome {Outcome}.")]
    internal static partial void SchemaCompleted(ILogger logger, LogLevel level, string operation, string outcome);
    /// <summary>Records a discovery or ownership boundary using safe correlation only.</summary>
    /// <param name="logger">The owner logger.</param><param name="operation">The closed discovery operation.</param><param name="tenantId">The captured tenant.</param><param name="principalId">The captured principal.</param><param name="agentId">The selected agent.</param><param name="sessionId">The selected session.</param><param name="runId">The active run.</param><param name="sourceId">The exact source for a source stage, otherwise null.</param>
    [LoggerMessage(4080, LogLevel.Debug, "Starting tool discovery operation {Operation} for tenant {TenantId}, principal {PrincipalId}, agent {AgentId}, session {SessionId}, run {RunId}, source {SourceId}.")]
    internal static partial void DiscoveryStarted(ILogger logger, string operation, TenantId tenantId, PrincipalId principalId, AgentId agentId, SessionId sessionId, RunId runId, ToolSourceId? sourceId);

    /// <summary>Records a closed discovery outcome without publication or exception content.</summary>
    /// <param name="logger">The owner logger.</param><param name="level">The outcome severity.</param><param name="operation">The closed discovery operation.</param><param name="tenantId">The captured tenant.</param><param name="principalId">The captured principal.</param><param name="agentId">The selected agent.</param><param name="sessionId">The selected session.</param><param name="runId">The active run.</param><param name="sourceId">The exact source for a source stage, otherwise null.</param><param name="outcome">The bounded terminal result.</param>
    [LoggerMessage(EventId = 4081, Message = "Completed tool discovery operation {Operation} for tenant {TenantId}, principal {PrincipalId}, agent {AgentId}, session {SessionId}, run {RunId}, source {SourceId} with outcome {Outcome}.")]
    internal static partial void DiscoveryCompleted(ILogger logger, LogLevel level, string operation, TenantId tenantId, PrincipalId principalId, AgentId agentId, SessionId sessionId, RunId runId, ToolSourceId? sourceId, string outcome);

    /// <summary>Records entry to complete registration selection without publication or alias content.</summary>
    /// <param name="logger">The registration catalog's logger.</param><param name="tenantId">The captured tenant.</param><param name="principalId">The captured principal.</param><param name="agentId">The selected agent.</param><param name="sessionId">The selected session.</param><param name="runId">The active run.</param>
    [LoggerMessage(4070, LogLevel.Debug, "Selecting tool registrations for tenant {TenantId}, principal {PrincipalId}, agent {AgentId}, session {SessionId}, run {RunId}.")]
    internal static partial void RegistrationSelectionStarted(ILogger logger, TenantId tenantId, PrincipalId principalId, AgentId agentId, SessionId sessionId, RunId runId);

    /// <summary>Records a bounded terminal registration outcome without untrusted metadata or exception text.</summary>
    /// <param name="logger">The registration catalog's logger.</param><param name="level">Severity matching the outcome.</param><param name="tenantId">The captured tenant.</param><param name="principalId">The captured principal.</param><param name="agentId">The selected agent.</param><param name="sessionId">The selected session.</param><param name="runId">The active run.</param><param name="outcome">The closed terminal selection result.</param>
    [LoggerMessage(EventId = 4071, Message = "Selected tool registrations for tenant {TenantId}, principal {PrincipalId}, agent {AgentId}, session {SessionId}, run {RunId} with outcome {Outcome}.")]
    internal static partial void RegistrationSelectionCompleted(ILogger logger, LogLevel level, TenantId tenantId, PrincipalId principalId, AgentId agentId, SessionId sessionId, RunId runId, string outcome);
    /// <summary>Records entry to a catalog merge boundary using identity evidence only.</summary>
    /// <param name="logger">The emitting runtime type's logger.</param><param name="operation">One of the two stable merge operation names.</param>
    /// <param name="tenantId">The captured tenant.</param><param name="principalId">The captured principal.</param><param name="agentId">The selected agent.</param><param name="sessionId">The selected session.</param><param name="runId">The active run.</param>
    [LoggerMessage(4060, LogLevel.Debug, "Starting {Operation} for tenant {TenantId}, principal {PrincipalId}, agent {AgentId}, session {SessionId}, run {RunId}.")]
    internal static partial void CatalogMergeStarted(ILogger logger, string operation, TenantId tenantId, PrincipalId principalId, AgentId agentId, SessionId sessionId, RunId runId);

    /// <summary>Records a bounded terminal merge result without copying aliases, schemas, or policy exception text.</summary>
    /// <param name="logger">The emitting runtime type's logger.</param><param name="level">Severity matching the semantic outcome.</param><param name="operation">One of the two stable merge operation names.</param>
    /// <param name="tenantId">The captured tenant.</param><param name="principalId">The captured principal.</param><param name="agentId">The selected agent.</param><param name="sessionId">The selected session.</param><param name="runId">The active run.</param><param name="outcome">A closed merge result.</param>
    [LoggerMessage(EventId = 4061, Message = "Completed {Operation} for tenant {TenantId}, principal {PrincipalId}, agent {AgentId}, session {SessionId}, run {RunId} with outcome {Outcome}.")]
    internal static partial void CatalogMergeCompleted(ILogger logger, LogLevel level, string operation, TenantId tenantId, PrincipalId principalId, AgentId agentId, SessionId sessionId, RunId runId, string outcome);
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
