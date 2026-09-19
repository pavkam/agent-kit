// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that pending-admission discovery could not be completed.</summary>
/// <remarks>This covers protected-access rejection, an unprovisioned or unrecognized lane, and any other failure that prevents a truthful pending-admission report. It never implies that no admissions are pending.</remarks>
public sealed record SessionPendingInputsUnavailable: SessionPendingInputsResult
{
    /// <summary>Initializes an unavailable pending-input discovery outcome.</summary>
    /// <param name="safeReason">The non-null, non-whitespace content-free explanation safe for callers and diagnostics.</param>
    /// <exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public SessionPendingInputsUnavailable(string safeReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        SafeReason = safeReason;
    }

    /// <summary>Gets the content-free explanation for the unavailable outcome.</summary>
    /// <value>A non-empty safe string that does not include admission payload or other protected content.</value>
    public string SafeReason { get; }
}
