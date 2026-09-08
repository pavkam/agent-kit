// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit;
/// <summary>Reports that an exact security-profile publication is unavailable.</summary>
public sealed record SecurityProfilePublicationUnavailable: SecurityProfilePublicationResult
{
    /// <summary>Initializes a safe unavailable-publication result.</summary><param name="safeReason">The nonblank caller-safe reason.</param><exception cref="ArgumentNullException"><paramref name="safeReason"/> is <see langword="null"/>.</exception><exception cref="ArgumentException"><paramref name="safeReason"/> is empty or whitespace.</exception>
    public SecurityProfilePublicationUnavailable(string safeReason) { ArgumentException.ThrowIfNullOrWhiteSpace(safeReason); SafeReason = safeReason; }
    /// <summary>Gets the caller-safe unavailable reason.</summary><value>Nonblank text without configuration or identity content.</value>
    public string SafeReason { get; }
}
