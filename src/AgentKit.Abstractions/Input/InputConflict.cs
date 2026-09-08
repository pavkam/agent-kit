// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports reuse of an input identity with non-equivalent canonical input, without replacing the original admission.</summary>
/// <remarks>The safe reason must remain content-free. This outcome carries no durable admission receipt because the conflicting attempt did not become a second admission.</remarks>
public sealed record InputConflict: InputAdmissionResult
{
    /// <summary>Initializes an idempotency conflict result.</summary>
    /// <param name="inputId">The non-default input identity reused with non-equivalent canonical input.</param>
    /// <param name="safeReason">A non-null, non-whitespace content-free explanation safe for callers and diagnostics.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="inputId"/> is default.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public InputConflict(InputId inputId, string safeReason)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(inputId, default); ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        InputId = inputId; SafeReason = safeReason;
    }
    /// <summary>Gets the input identity whose reuse caused the conflict.</summary>
    /// <value>A non-default caller-visible idempotency identity; it does not expose the original or attempted payload.</value>
    public InputId InputId { get; }
    /// <summary>Gets the content-free explanation of the conflict class.</summary>
    /// <value>A non-empty safe string suitable for callers and diagnostics, with no user or model payload.</value>
    public string SafeReason { get; }
}
