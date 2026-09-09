// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Stores durable safe error evidence for a terminal tool result.</summary>
/// <remarks>It intentionally excludes exceptions, stack traces, bodies, and secret-bearing diagnostics.</remarks>
public sealed record ToolError
{
    /// <summary>Initializes safe error evidence.</summary>
    /// <param name="kind">Defined error-source classification.</param>
    /// <param name="safeMessage">Nonblank durable safe explanation.</param>
    /// <param name="externalCode">Optional safe external code.</param>
    /// <param name="retryAfter">Nonnegative reported retry delay.</param>
    /// <param name="extensions">Compatible immutable evidence.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined or <paramref name="retryAfter"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> or a present <paramref name="externalCode"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public ToolError(ToolErrorKind kind, string safeMessage, string? externalCode, TimeSpan? retryAfter, ExtensionData extensions)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        if (externalCode is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(externalCode);
        }
        if (retryAfter is { } delay)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero, nameof(retryAfter));
        }
        ArgumentNullException.ThrowIfNull(extensions);
        Kind = kind; SafeMessage = safeMessage; ExternalCode = externalCode; RetryAfter = retryAfter; Extensions = extensions;
    }
    /// <summary>Gets the error source classification.</summary>
    /// <value>A defined durable classification.</value>
    public ToolErrorKind Kind { get; }
    /// <summary>Gets the safe explanation.</summary>
    /// <value>Nonblank content selected for durable use.</value>
    public string SafeMessage { get; }
    /// <summary>Gets the optional external code.</summary>
    /// <value>Null when none was safely reported.</value>
    public string? ExternalCode { get; }
    /// <summary>Gets the optional retry delay.</summary>
    /// <value>Null or a nonnegative duration.</value>
    public TimeSpan? RetryAfter { get; }
    /// <summary>Gets compatible extension evidence.</summary>
    /// <value>A nonnull immutable bag.</value>
    public ExtensionData Extensions { get; }
}
