// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Binds one <see cref="IToolExecutor.ExecuteAsync"/> batch to its exact session and budget execution capabilities.</summary>
/// <remarks>
/// This is invocation-only evidence, never a service locator: it binds execution to the run's exact session
/// profile/coordinators and budget profile/scope without exposing a broader lookup surface. Ship note: this is a
/// deliberately reduced stand-in that omits the spec's <c>ExecutionPolicies</c> binding array
/// (<c>ImmutableArray&lt;ToolExecutionPolicyBinding&gt;</c>); that field lands with the execution-policy chunks
/// (workstream 4, chunks C5a onward) that introduce <c>IToolExecutionPolicy</c>. This type is an immutable value
/// object with structural equality over its fields and is safe to share across threads without synchronization.
/// </remarks>
public sealed record ToolExecutionCapability
{
    /// <summary>Initializes an immutable tool-execution capability.</summary>
    /// <param name="session">The nonnull invocation-only session execution capability.</param>
    /// <param name="budget">The nonnull invocation-only budget execution capability.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="budget"/> is null.</exception>
    public ToolExecutionCapability(SessionExecutionCapability session, BudgetExecutionCapability budget)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(budget);
        Session = session;
        Budget = budget;
    }

    /// <summary>Gets the invocation-only session execution capability.</summary>
    /// <value>The compiled session profile and borrowed coordinator instances for this batch.</value>
    public SessionExecutionCapability Session { get; }

    /// <summary>Gets the invocation-only budget execution capability.</summary>
    /// <value>The selected budget profile and borrowed live scope for this batch.</value>
    public BudgetExecutionCapability Budget { get; }
}
