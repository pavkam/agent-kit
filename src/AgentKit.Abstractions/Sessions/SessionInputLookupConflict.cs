// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports reuse of an input identity with different immutable admission evidence.</summary>
public sealed record SessionInputLookupConflict: SessionInputLookupResult
{
    /// <summary>Initializes a content-free conflict.</summary><param name="inputId">The conflicting input identity.</param><param name="safeReason">A nonblank safe explanation.</param><exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public SessionInputLookupConflict(InputId inputId, string safeReason) { ArgumentOutOfRangeException.ThrowIfEqual(inputId, default); ArgumentException.ThrowIfNullOrWhiteSpace(safeReason); InputId = inputId; SafeReason = safeReason; }
    /// <summary>Gets the conflicting input identity.</summary><value>A non-default caller idempotency identity.</value>
    public InputId InputId { get; }
    /// <summary>Gets the content-free reason.</summary><value>A safe nonblank explanation.</value>
    public string SafeReason { get; }
}
