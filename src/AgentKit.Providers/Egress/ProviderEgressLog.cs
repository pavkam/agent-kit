// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Egress;

/// <summary>Content-free structured log events for provider egress; fields are bounded identifiers and never request content.</summary>
internal static partial class ProviderEgressLog
{
    [LoggerMessage(
        6120,
        LogLevel.Debug,
        "Provider {ProviderId} {Operation} egress was admitted and answered with HTTP status {StatusCode}.")]
    internal static partial void Sent(ILogger logger, ProviderId providerId, ProviderEgressOperation operation, int statusCode);

    [LoggerMessage(
        6121,
        LogLevel.Warning,
        "Provider {ProviderId} {Operation} egress was refused at stage {Stage} with {FailureKind}.")]
    internal static partial void Refused(
        ILogger logger,
        ProviderId providerId,
        ProviderEgressOperation operation,
        string stage,
        ProviderFailureKind failureKind);

    [LoggerMessage(
        6122,
        LogLevel.Debug,
        "Provider {ProviderId} {Operation} egress was cancelled at stage {Stage}.")]
    internal static partial void Cancelled(
        ILogger logger,
        ProviderId providerId,
        ProviderEgressOperation operation,
        string stage);

    [LoggerMessage(
        6123,
        LogLevel.Debug,
        "Provider {ProviderId} {Operation} credential was released under a credential-read grant and applied.")]
    internal static partial void CredentialApplied(ILogger logger, ProviderId providerId, ProviderEgressOperation operation);
}
