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
}
