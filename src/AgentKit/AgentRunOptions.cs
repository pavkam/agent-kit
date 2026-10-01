// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The bounded per-invocation overrides a caller may apply on top of an agent
/// definition's configured run policy.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object. It carries no mutable state and is
/// safe to share across threads without synchronization.
/// </para>
/// <para>
/// Session, conversation, execution identity, and input are separate,
/// explicit parameters on typed <see cref="Agent"/> run requests and the facade
/// <see cref="AgentRunRequest"/> rather than fields here: this type carries
/// only the bounded overrides, so the same immutable instance can accompany
/// any invocation regardless of who runs it or on which session.
/// </para>
/// <para>
/// Overrides are bounded and may only narrow the definition's limits. A run
/// cannot widen <see cref="RunPolicyDefaults.MaxTurns"/> or the attempt
/// timeout beyond what the definition allows, so an agent's configured
/// ceiling stays a ceiling.
/// </para>
/// <para>
/// <see cref="AllowedTools"/> narrows the tool surface the same way: the run's captured catalog is intersected with the list,
/// so a tool outside it is neither advertised to the model nor resolvable, and a call naming one is rejected as an unknown tool.
/// An empty list exposes no tool. <see cref="BudgetParentScopeId"/> places the run's own budget scope beneath an existing
/// scope (for example a delegated goal's reserved child scope), so the run is bounded by that scope's remaining capacity as
/// well as by its own budget profile.
/// </para>
/// </remarks>
public sealed record AgentRunOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentRunOptions"/> record.
    /// </summary>
    /// <param name="maxTurns">
    /// An optional lower turn limit for this run. Must not exceed the
    /// definition's default.
    /// </param>
    /// <param name="attemptTimeout">
    /// An optional shorter attempt timeout for this run. Must not exceed the
    /// definition's default.
    /// </param>
    /// <param name="allowedTools">
    /// An optional tool allow-list for this run, or <see langword="null"/> to keep the definition's whole tool surface. An
    /// empty array runs the agent with no tools.
    /// </param>
    /// <param name="budgetParentScopeId">
    /// An optional existing budget scope the run's own scope is created beneath, or <see langword="null"/> for a root run
    /// scope.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A supplied <paramref name="maxTurns"/> or <paramref name="attemptTimeout"/>
    /// is zero or negative, or <paramref name="budgetParentScopeId"/> is the default value.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="allowedTools"/> is the default array or contains a default tool identifier.</exception>
    public AgentRunOptions(
        int? maxTurns = null,
        TimeSpan? attemptTimeout = null,
        ImmutableArray<ToolId>? allowedTools = null,
        BudgetScopeId? budgetParentScopeId = null)
    {
        if (allowedTools is { } tools)
        {
            ArgumentException.ThrowIfDefault(tools, nameof(allowedTools));
            foreach (var tool in tools)
            {
                ArgumentOutOfRangeException.ThrowIfEqual(tool, default, nameof(allowedTools));
            }
        }

        if (budgetParentScopeId is { } parentScope)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(parentScope, default, nameof(budgetParentScopeId));
        }

        if (maxTurns is { } turns)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(turns, nameof(maxTurns));
        }

        if (attemptTimeout is { } timeout)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
                timeout,
                TimeSpan.Zero,
                nameof(attemptTimeout));
        }

        MaxTurns = maxTurns;
        AttemptTimeout = attemptTimeout;
        AllowedTools = allowedTools;
        BudgetParentScopeId = budgetParentScopeId;
    }

    /// <summary>
    /// Gets the optional narrowed turn limit, or <see langword="null"/> to use
    /// the definition's default.
    /// </summary>
    public int? MaxTurns { get; init; }

    /// <summary>
    /// Gets the optional narrowed attempt timeout, or <see langword="null"/>
    /// to use the definition's default.
    /// </summary>
    public TimeSpan? AttemptTimeout { get; init; }

    /// <summary>Gets the optional per-run tool allow-list.</summary>
    /// <value>The tool identifiers the run may see and call, or <see langword="null"/> for the definition's whole surface.</value>
    public ImmutableArray<ToolId>? AllowedTools { get; init; }

    /// <summary>Gets the optional budget scope the run's own scope is created beneath.</summary>
    /// <value>An existing scope identifier, or <see langword="null"/> for a root run scope.</value>
    public BudgetScopeId? BudgetParentScopeId { get; init; }
}
