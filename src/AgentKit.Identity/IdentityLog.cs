// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity;

/// <summary>Defines safe structured logs for identity terminal outcomes.</summary>
internal static partial class IdentityLog
{
    [LoggerMessage(17000, LogLevel.Debug, "Identity resolution completed with outcome {Outcome}.")]
    internal static partial void Resolved(ILogger logger, string outcome);

    [LoggerMessage(17001, LogLevel.Debug, "Identity derivation completed with outcome {Outcome}.")]
    internal static partial void Derived(ILogger logger, string outcome);

    [LoggerMessage(17002, LogLevel.Debug, "Identity resolution was cancelled.")]
    internal static partial void ResolveCancelled(ILogger logger);

    [LoggerMessage(17003, LogLevel.Error, "Identity resolution failed with error type {ErrorType}.")]
    internal static partial void ResolveFailed(ILogger logger, string errorType);

    [LoggerMessage(17004, LogLevel.Error, "Identity derivation failed with error type {ErrorType}.")]
    internal static partial void DeriveFailed(ILogger logger, string errorType);

    [LoggerMessage(17005, LogLevel.Debug, "Identity derivation was cancelled.")]
    internal static partial void DeriveCancelled(ILogger logger);
}
