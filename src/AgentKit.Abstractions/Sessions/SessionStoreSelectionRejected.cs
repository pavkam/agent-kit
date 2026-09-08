// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that no store may be selected safely for the supplied routing evidence.</summary>
/// <remarks>The safe message must not disclose whether a tenant-masked session exists or expose provider and storage diagnostics.</remarks>
public sealed record SessionStoreSelectionRejected: SessionStoreSelectionResult
{
    /// <summary>Initializes a typed selection rejection.</summary>
    /// <param name="reason">The defined reason callers use for recovery decisions.</param>
    /// <param name="safeMessage">The nonblank caller-safe diagnostic text.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reason"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public SessionStoreSelectionRejected(SessionStoreSelectionRejectionReason reason, string safeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        Reason = reason;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the typed rejection reason.</summary><value>The defined reason used to choose a safe recovery action.</value>
    public SessionStoreSelectionRejectionReason Reason { get; }
    /// <summary>Gets the caller-safe diagnostic.</summary><value>Nonblank text containing no protected state or storage diagnostics.</value>
    public string SafeMessage { get; }
}
