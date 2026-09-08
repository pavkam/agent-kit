// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports denial or unavailability before admitted-input state was disclosed.</summary>
public sealed record SessionInputLookupRejected: SessionInputLookupResult
{
    /// <summary>Initializes a safe lookup rejection.</summary><param name="safeReason">A nonblank content-free reason.</param><exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public SessionInputLookupRejected(string safeReason) { ArgumentException.ThrowIfNullOrWhiteSpace(safeReason); SafeReason = safeReason; }
    /// <summary>Gets the content-free reason.</summary><value>A safe nonblank explanation.</value>
    public string SafeReason { get; }
}
