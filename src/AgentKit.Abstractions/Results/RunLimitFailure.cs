// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Combines exact budget exhaustion with the enforcement boundary and retained partial-effect evidence.</summary>
/// <remarks>Observed usage is never clamped to the configured ceiling. This value describes a limit failure; it neither reserves capacity nor rewrites committed output.</remarks>
public sealed record RunLimitFailure
{
    /// <summary>Captures a structurally valid limit observation and its run consequences.</summary>
    /// <param name="limit">The nonnull limit with nondefault scope, dimension and unit, defined kind, nonnegative ceiling and nonblank safe message.</param>
    /// <param name="enforcementBoundary">The nondefault component that enforced the limit.</param>
    /// <param name="hasPartialOutput">Whether truthful partial output exists.</param>
    /// <param name="sideEffectCertainty">The defined certainty of relevant effects already attempted.</param>
    /// <exception cref="ArgumentNullException">The limit or its safe message is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A required identity is default, a kind or certainty is undefined, or the ceiling is negative.</exception>
    /// <exception cref="ArgumentException">The limit's safe message is blank.</exception>
    public RunLimitFailure(BudgetLimitFailure limit, ComponentId enforcementBoundary, bool hasPartialOutput, SideEffectCertainty sideEffectCertainty)
    {
        ArgumentNullException.ThrowIfNull(limit);
        ArgumentOutOfRangeException.ThrowIfEqual(limit.ScopeId, default, nameof(limit));
        ArgumentOutOfRangeException.ThrowIfEqual(limit.Dimension, default, nameof(limit));
        ArgumentOutOfRangeException.ThrowIfEqual(limit.Unit, default, nameof(limit));
        ArgumentOutOfRangeException.ThrowIfUndefined(limit.Kind, nameof(limit));
        ArgumentOutOfRangeException.ThrowIfNegative(limit.ConfiguredValue, nameof(limit));
        ArgumentException.ThrowIfNullOrWhiteSpace(limit.SafeMessage, nameof(limit));
        ArgumentOutOfRangeException.ThrowIfEqual(enforcementBoundary, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(sideEffectCertainty);
        Limit = limit; EnforcementBoundary = enforcementBoundary; HasPartialOutput = hasPartialOutput; SideEffectCertainty = sideEffectCertainty;
    }
    /// <summary>Gets exact configured, observed, requested, dimension and unit evidence.</summary>
    /// <value>A nonnull structurally validated limit; observed quantities retain their full magnitude.</value>
    public BudgetLimitFailure Limit { get; }
    /// <summary>Gets the component that enforced the limit.</summary>
    /// <value>A nondefault boundary identity.</value>
    public ComponentId EnforcementBoundary { get; }
    /// <summary>Gets whether output already exists and must remain available.</summary>
    /// <value>True when the owner retained truthful partial output.</value>
    public bool HasPartialOutput { get; }
    /// <summary>Gets effect certainty independently of partial output.</summary>
    /// <value>A defined certainty; unknown effects never become safe to replay because a limit was reached.</value>
    public SideEffectCertainty SideEffectCertainty { get; }
}
