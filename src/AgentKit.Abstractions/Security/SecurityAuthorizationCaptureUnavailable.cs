// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit;
/// <summary>Reports that exact authorization capture could not complete without a fallback.</summary>
public sealed record SecurityAuthorizationCaptureUnavailable: SecurityAuthorizationCaptureResult
{
    /// <summary>Initializes a safe unavailable capture result.</summary><param name="safeReason">The nonblank caller-safe reason.</param><exception cref="ArgumentNullException"><paramref name="safeReason"/> is <see langword="null"/>.</exception><exception cref="ArgumentException"><paramref name="safeReason"/> is empty or whitespace.</exception>
    public SecurityAuthorizationCaptureUnavailable(string safeReason) { ArgumentException.ThrowIfNullOrWhiteSpace(safeReason); SafeReason = safeReason; }
    /// <summary>Gets the caller-safe unavailable reason.</summary><value>Nonblank text with no policy, configuration, or identity detail.</value>
    public string SafeReason { get; }
}
