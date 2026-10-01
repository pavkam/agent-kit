// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Binds one <see cref="IToolExecutor.ExecuteAsync"/> batch to its exact session, budget, and execution-policy capabilities.</summary>
/// <remarks>
/// This is invocation-only evidence, never a service locator: it binds execution to the run's exact session
/// profile/coordinators, the branch and lane that receive the call's durable records, the budget profile/scope, and the
/// execution-policy references the run's catalog captured, without exposing a broader lookup surface. The executor
/// rejects any call whose captured policy reference is not among <see cref="ExecutionPolicies"/>. This type is an
/// immutable value object with structural equality over its fields and is safe to share across threads without
/// synchronization.
/// </remarks>
public sealed record ToolExecutionCapability
{
    /// <summary>Initializes an immutable tool-execution capability.</summary>
    /// <param name="session">The nonnull invocation-only session execution capability.</param>
    /// <param name="budget">The invocation-only budget execution capability, or <see langword="null"/> for a run that reserves no tool budget dimensions.</param>
    /// <param name="sessionTarget">The nonnull branch and lane that receive the batch's accepted and terminal records.</param>
    /// <param name="executionPolicies">The initialized execution-policy references the run's catalog captured; may be empty, which permits no call.</param>
    /// <param name="hooks">Optional hook binding for before-invocation and result points; null when hooks are inactive.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/>, <paramref name="sessionTarget"/>, or a binding is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="executionPolicies"/> is uninitialized or names one reference twice.</exception>
    public ToolExecutionCapability(
        SessionExecutionCapability session,
        BudgetExecutionCapability? budget,
        ToolCallSessionTarget sessionTarget,
        ImmutableArray<ToolExecutionPolicyBinding> executionPolicies,
        ToolExecutionHookBinding? hooks = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sessionTarget);
        ArgumentException.ThrowIfDefault(executionPolicies);
        ArgumentException.ThrowIfContainsNull(executionPolicies);
        var references = new HashSet<ToolExecutionPolicyReference>();
        foreach (var binding in executionPolicies)
        {
            ArgumentException.ThrowIfNotEqual(references.Add(binding.Reference), true, nameof(executionPolicies));
        }

        Session = session;
        Budget = budget;
        SessionTarget = sessionTarget;
        ExecutionPolicies = executionPolicies;
        Hooks = hooks;
    }

    /// <summary>Gets the invocation-only session execution capability.</summary>
    /// <value>The compiled session profile and borrowed coordinator instances for this batch.</value>
    public SessionExecutionCapability Session { get; }

    /// <summary>Gets the invocation-only budget execution capability.</summary>
    /// <value>The selected budget profile and borrowed live scope for this batch, or <see langword="null"/> when the run reserves no tool budget dimensions; the executor then performs no tool budget reservation.</value>
    public BudgetExecutionCapability? Budget { get; }

    /// <summary>Gets the branch and lane that receive the batch's durable tool-call records.</summary>
    /// <value>A position inside <see cref="Session"/>'s session; appends still pass the coordinator's own checks.</value>
    public ToolCallSessionTarget SessionTarget { get; }

    /// <summary>Gets the execution-policy references the run may use.</summary>
    /// <value>Unique exact references captured with the run's catalog; the selector matches against these only.</value>
    public ImmutableArray<ToolExecutionPolicyBinding> ExecutionPolicies { get; }

    /// <summary>Gets the optional hook binding for tool lifecycle points handled by the executor.</summary>
    /// <value>Null when no hook scope is active for this batch.</value>
    public ToolExecutionHookBinding? Hooks { get; }

    /// <summary>Determines structural equality including the ordered policy bindings.</summary>
    /// <param name="other">The capability to compare, or null.</param>
    /// <returns><see langword="true"/> when every field and the ordered bindings are equal.</returns>
    public bool Equals(ToolExecutionCapability? other) =>
        other is not null
        && Session == other.Session
        && Budget == other.Budget
        && SessionTarget == other.SessionTarget
        && ExecutionPolicies.SequenceEqual(other.ExecutionPolicies)
        && Hooks == other.Hooks;

    /// <summary>Returns a hash compatible with <see cref="Equals(ToolExecutionCapability?)"/>.</summary>
    /// <returns>A hash over every field and the ordered bindings.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Session);
        hash.Add(Budget);
        hash.Add(SessionTarget);
        foreach (var binding in ExecutionPolicies)
        {
            hash.Add(binding);
        }

        hash.Add(Hooks);
        return hash.ToHashCode();
    }
}
