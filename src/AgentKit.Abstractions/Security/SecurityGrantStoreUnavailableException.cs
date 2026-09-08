// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that authoritative grant state could not be read or committed without fabricating a grant-consumption status.</summary>
/// <remarks>The safe message excludes provider details and protected evidence. The optional inner exception is diagnostic context and must not be exported without host redaction.</remarks>
public sealed class SecurityGrantStoreUnavailableException: Exception
{
    /// <summary>Initializes a bounded grant-store infrastructure failure.</summary>
    /// <param name="kind">The defined failure class.</param>
    /// <param name="safeMessage">A nonblank redacted description suitable for application control flow.</param>
    /// <param name="innerException">Optional provider diagnostic context retained in process.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is null, empty, or whitespace.</exception>
    public SecurityGrantStoreUnavailableException(
        SecurityGrantStoreFailureKind kind,
        string safeMessage,
        Exception? innerException = null)
        : base(ValidateMessage(kind, safeMessage), innerException)
    {
        Kind = kind;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the bounded infrastructure failure class.</summary>
    /// <value>A defined value safe for application control flow and bounded diagnostics.</value>
    public SecurityGrantStoreFailureKind Kind { get; }

    /// <summary>Gets the redacted failure description.</summary>
    /// <value>A nonblank message containing no provider target, protected evidence, or raw provider error.</value>
    public string SafeMessage { get; }

    private static string ValidateMessage(SecurityGrantStoreFailureKind kind, string safeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        return safeMessage;
    }
}
