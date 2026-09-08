// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that the authority exactly named by captured evidence is unavailable.</summary>
/// <remarks>
/// This outcome is fail-closed. Callers must not substitute an unkeyed, newer, or differently keyed
/// authority for <see cref="Authorization"/>.
/// </remarks>
public sealed record SecurityAuthoritySelectionUnavailable: SecurityAuthoritySelectionResult
{
    /// <summary>Initializes a content-free failed authority activation.</summary>
    /// <param name="authorization">The immutable context whose exact authority could not be activated.</param>
    /// <param name="safeReason">A non-empty content-free explanation safe for callers and diagnostics.</param>
    /// <exception cref="ArgumentNullException"><paramref name="authorization"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public SecurityAuthoritySelectionUnavailable(SecurityAuthorizationContext authorization, string safeReason)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        Authorization = authorization;
        SafeReason = safeReason;
    }

    /// <summary>Gets the immutable context whose exact authority binding was unavailable.</summary>
    /// <value>The original captured context, retained without any fallback selection.</value>
    public SecurityAuthorizationContext Authorization { get; }

    /// <summary>Gets a content-free explanation of the unavailable captured binding.</summary>
    /// <value>A non-empty safe message that does not contain protected input or policy internals.</value>
    public string SafeReason { get; }
}
