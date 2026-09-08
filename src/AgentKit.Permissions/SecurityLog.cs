// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Defines allocation-efficient structured security-authority log events.</summary>
internal static partial class SecurityLog
{
    /// <summary>Logs the start of a normalized authorization request without protected resources.</summary>
    [LoggerMessage(5000, LogLevel.Debug, "Authorizing security request {SecurityRequestId} for operation {OperationKind} and effect {Effect}.")]
    internal static partial void AuthorizationStarted(
        ILogger logger,
        SecurityRequestId securityRequestId,
        SecurityOperationKind operationKind,
        SecurityEffect effect);

    /// <summary>Logs a bounded allow decision and its grant identity.</summary>
    [LoggerMessage(5001, LogLevel.Debug, "Allowed security request {SecurityRequestId} with grant {GrantId}.")]
    internal static partial void AuthorizationAllowed(ILogger logger, SecurityRequestId securityRequestId, GrantId grantId);

    /// <summary>Logs a deny decision without policy internals or protected resources.</summary>
    [LoggerMessage(5002, LogLevel.Information, "Denied security request {SecurityRequestId}.")]
    internal static partial void AuthorizationDenied(ILogger logger, SecurityRequestId securityRequestId);

    /// <summary>Logs caller cancellation of an authorization request.</summary>
    [LoggerMessage(5003, LogLevel.Information, "Cancelled security request {SecurityRequestId}.")]
    internal static partial void AuthorizationCancelled(ILogger logger, SecurityRequestId securityRequestId);

    /// <summary>Logs an unexpected policy or grant-store error type without protected resource details.</summary>
    [LoggerMessage(5004, LogLevel.Error, "Security request {SecurityRequestId} faulted during authorization with error type {ErrorType}.")]
    internal static partial void AuthorizationFaulted(
        ILogger logger,
        SecurityRequestId securityRequestId,
        string errorType);

    /// <summary>Logs exact-key authority selection without granting authority.</summary>
    [LoggerMessage(5005, LogLevel.Debug, "Selecting captured security authority {SecurityAuthorityKey}.")]
    internal static partial void AuthoritySelectionStarted(ILogger logger, ComponentKey<ISecurityAuthority> securityAuthorityKey);

    /// <summary>Logs successful exact-key authority activation without issuing a grant.</summary>
    [LoggerMessage(5006, LogLevel.Debug, "Selected captured security authority {SecurityAuthorityKey}.")]
    internal static partial void AuthoritySelected(ILogger logger, ComponentKey<ISecurityAuthority> securityAuthorityKey);

    /// <summary>Logs fail-closed unavailability of one captured authority binding.</summary>
    [LoggerMessage(5007, LogLevel.Information, "Captured security authority {SecurityAuthorityKey} is unavailable.")]
    internal static partial void AuthorityUnavailable(ILogger logger, ComponentKey<ISecurityAuthority> securityAuthorityKey);

    /// <summary>Logs caller cancellation before a captured authority selection completes.</summary>
    [LoggerMessage(5008, LogLevel.Information, "Selecting captured security authority {SecurityAuthorityKey} was cancelled.")]
    internal static partial void AuthoritySelectionCancelled(ILogger logger, ComponentKey<ISecurityAuthority> securityAuthorityKey);

    /// <summary>Logs an unexpected authority-selection failure without protected data.</summary>
    [LoggerMessage(5009, LogLevel.Error, "Selecting captured security authority {SecurityAuthorityKey} faulted with error type {ErrorType}.")]
    internal static partial void AuthoritySelectionFaulted(ILogger logger, ComponentKey<ISecurityAuthority> securityAuthorityKey, string errorType);

    /// <summary>Logs the start of delivery for one immutable audit record without its protected field values.</summary>
    /// <param name="logger">The content-free logger to receive the structured event.</param>
    /// <param name="securityAuditRecordId">The correlation identity of the record being delivered.</param>
    /// <param name="securityAuditEventKind">The bounded transition classification declared by the record.</param>
    /// <remarks>The event deliberately excludes redacted field values, request resources, and sink details.</remarks>
    [LoggerMessage(5010, LogLevel.Debug, "Dispatching security audit record {SecurityAuditRecordId} with event kind {SecurityAuditEventKind}.")]
    internal static partial void AuditDispatchStarted(
        ILogger logger,
        SecurityAuditRecordId securityAuditRecordId,
        SecurityAuditEventKind securityAuditEventKind);

    /// <summary>Logs audit delivery after its configured acceptance requirement was satisfied.</summary>
    /// <param name="logger">The content-free logger to receive the structured event.</param>
    /// <param name="securityAuditRecordId">The correlation identity of the record accepted for delivery.</param>
    /// <remarks>Acceptance describes audit delivery only; it does not assert that a protected effect completed.</remarks>
    [LoggerMessage(5011, LogLevel.Debug, "Security audit record {SecurityAuditRecordId} was accepted for delivery.")]
    internal static partial void AuditDispatchAccepted(ILogger logger, SecurityAuditRecordId securityAuditRecordId);

    /// <summary>Logs that a required audit delivery could not find a compatible durable acceptance path.</summary>
    /// <param name="logger">The content-free logger to receive the structured event.</param>
    /// <param name="securityAuditRecordId">The correlation identity of the record without a durable path.</param>
    /// <remarks>The event includes no sink configuration, field values, or protected resources.</remarks>
    [LoggerMessage(5012, LogLevel.Information, "Security audit record {SecurityAuditRecordId} has no compatible durable delivery path.")]
    internal static partial void AuditDispatchUnavailable(ILogger logger, SecurityAuditRecordId securityAuditRecordId);

    /// <summary>Logs that required audit delivery failed without exposing a sink exception message.</summary>
    /// <param name="logger">The content-free logger to receive the structured event.</param>
    /// <param name="securityAuditRecordId">The correlation identity of the record whose required delivery failed.</param>
    /// <param name="errorType">The normalized exception type name, never an exception message or sink payload.</param>
    /// <remarks>The error type is diagnostic classification only and does not include protected content.</remarks>
    [LoggerMessage(5013, LogLevel.Error, "Security audit record {SecurityAuditRecordId} failed delivery with error type {ErrorType}.")]
    internal static partial void AuditDispatchFailed(ILogger logger, SecurityAuditRecordId securityAuditRecordId, string errorType);

    /// <summary>Logs caller cancellation before audit delivery reached a terminal dispatch result.</summary>
    /// <param name="logger">The content-free logger to receive the structured event.</param>
    /// <param name="securityAuditRecordId">The correlation identity of the record whose dispatch was cancelled.</param>
    /// <remarks>A prior sink acceptance remains an external fact and is not represented as a completed dispatch result.</remarks>
    [LoggerMessage(5014, LogLevel.Information, "Security audit record {SecurityAuditRecordId} delivery was cancelled.")]
    internal static partial void AuditDispatchCancelled(ILogger logger, SecurityAuditRecordId securityAuditRecordId);

    /// <summary>Logs an isolated best-effort sink failure without exposing the sink's exception message.</summary>
    /// <param name="logger">The content-free logger to receive the structured event.</param>
    /// <param name="securityAuditRecordId">The correlation identity of the record whose optional delivery failed.</param>
    /// <param name="errorType">The normalized exception type name, never an exception message or sink payload.</param>
    /// <remarks>The failure is observationally recorded while the configured best-effort delivery continues.</remarks>
    [LoggerMessage(5015, LogLevel.Warning, "Best-effort delivery of security audit record {SecurityAuditRecordId} failed with error type {ErrorType}.")]
    internal static partial void BestEffortAuditDispatchFailed(ILogger logger, SecurityAuditRecordId securityAuditRecordId, string errorType);

    /// <summary>Logs a required audit-delivery deadline whose durable acceptance status remains unknown.</summary>
    /// <param name="logger">The content-free logger to receive the structured event.</param>
    /// <param name="securityAuditRecordId">The stable correlation identity of the record whose delivery timed out.</param>
    /// <remarks>The event does not claim non-persistence and excludes sink payloads, exception messages, and protected content.</remarks>
    [LoggerMessage(5024, LogLevel.Error, "Required delivery of security audit record {SecurityAuditRecordId} timed out; durable acceptance is unknown.")]
    internal static partial void AuditDispatchTimedOut(ILogger logger, SecurityAuditRecordId securityAuditRecordId);

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

    /// <summary>Logs the start of exact security-profile capture without policy or identity content.</summary>
    /// <param name="logger">The content-free logger that receives the structured event.</param>
    /// <param name="securityProfileKey">The explicit profile key requested from the immutable publication.</param>
    /// <remarks>The event excludes policy fingerprints, execution identity, and publication content.</remarks>
    [LoggerMessage(5016, LogLevel.Debug, "Capturing exact security profile {SecurityProfileKey}.")]
    internal static partial void ProfileCaptureStarted(ILogger logger, SecurityProfileKey securityProfileKey);

    /// <summary>Logs successful exact security-profile capture without granting authority.</summary>
    /// <param name="logger">The content-free logger that receives the structured event.</param>
    /// <param name="securityProfileKey">The exact profile key captured into authorization evidence.</param>
    /// <remarks>Capture reports immutable evidence only; it does not report an allow decision or grant.</remarks>
    [LoggerMessage(5017, LogLevel.Debug, "Captured exact security profile {SecurityProfileKey}.")]
    internal static partial void ProfileCaptured(ILogger logger, SecurityProfileKey securityProfileKey);

    /// <summary>Logs fail-closed publication unavailability with a bounded diagnostic outcome.</summary>
    /// <param name="logger">The content-free logger that receives the structured event.</param>
    /// <param name="securityProfileKey">The exact profile key whose publication could not be captured.</param>
    /// <param name="outcome">The bounded unavailable or mismatched-publication outcome.</param>
    /// <remarks>The event does not include reader reasons or mismatched publication coordinates.</remarks>
    [LoggerMessage(5018, LogLevel.Information, "Exact security profile {SecurityProfileKey} is unavailable with outcome {Outcome}.")]
    internal static partial void ProfileCaptureUnavailable(
        ILogger logger,
        SecurityProfileKey securityProfileKey,
        string outcome);

    /// <summary>Logs caller cancellation before exact security-profile capture completes.</summary>
    /// <param name="logger">The content-free logger that receives the structured event.</param>
    /// <param name="securityProfileKey">The exact profile key whose capture was cancelled.</param>
    [LoggerMessage(5019, LogLevel.Information, "Capturing exact security profile {SecurityProfileKey} was cancelled.")]
    internal static partial void ProfileCaptureCancelled(ILogger logger, SecurityProfileKey securityProfileKey);

    /// <summary>Logs an unexpected profile-capture failure without policy or identity content.</summary>
    /// <param name="logger">The content-free logger that receives the structured event.</param>
    /// <param name="securityProfileKey">The exact profile key whose capture faulted.</param>
    /// <param name="errorType">The normalized exception type name without its message or protected content.</param>
    [LoggerMessage(5020, LogLevel.Error, "Capturing exact security profile {SecurityProfileKey} faulted with error type {ErrorType}.")]
    internal static partial void ProfileCaptureFaulted(
        ILogger logger,
        SecurityProfileKey securityProfileKey,
        string errorType);

    /// <summary>Logs that an exact publication read found its immutable publication.</summary>
    /// <param name="logger">The content-free logger that receives the structured event.</param>
    /// <param name="securityProfileKey">The exact profile key requested from the frozen publication map.</param>
    [LoggerMessage(5021, LogLevel.Debug, "Found exact security-profile publication {SecurityProfileKey}.")]
    internal static partial void ProfilePublicationFound(
        ILogger logger,
        SecurityProfileKey securityProfileKey);

    /// <summary>Logs that an exact publication read found no matching immutable publication.</summary>
    /// <param name="logger">The content-free logger that receives the structured event.</param>
    /// <param name="securityProfileKey">The exact profile key unavailable from the frozen publication map.</param>
    [LoggerMessage(5022, LogLevel.Information, "Exact security-profile publication {SecurityProfileKey} is unavailable.")]
    internal static partial void ProfilePublicationUnavailable(
        ILogger logger,
        SecurityProfileKey securityProfileKey);

    /// <summary>Logs caller cancellation before an exact publication read completes.</summary>
    /// <param name="logger">The content-free logger that receives the structured event.</param>
    /// <param name="securityProfileKey">The exact profile key whose publication read was cancelled.</param>
    [LoggerMessage(5023, LogLevel.Information, "Reading exact security-profile publication {SecurityProfileKey} was cancelled.")]
    internal static partial void ProfilePublicationReadCancelled(
        ILogger logger,
        SecurityProfileKey securityProfileKey);
}
