// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Internal;

using AgentKit;

/// <summary>Owns one run scope for as long as the compiled plan is in use.</summary>
/// <remarks>
/// Disposal releases the scope and nothing else. It does not cancel a run that has already been handed to a
/// caller, and it does not append session records.
/// </remarks>
internal sealed class AgentRunScopeLease: IAsyncDisposable
{
    private readonly IAsyncDisposable _ownedScope;
    private readonly IServiceProvider _provider;

    /// <summary>Captures a compiled plan and the scope that produced it.</summary>
    /// <param name="plan">The non-null plan resolved from the scope.</param>
    /// <param name="ownedScope">The scope to dispose exactly once when this lease is disposed.</param>
    /// <param name="provider">The scope provider, used only to publish <see cref="RunScopeIdentity"/> after a run is accepted.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    internal AgentRunScopeLease(AgentRunPlan plan, IAsyncDisposable ownedScope, IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(ownedScope);
        ArgumentNullException.ThrowIfNull(provider);
        Plan = plan;
        _ownedScope = ownedScope;
        _provider = provider;
    }

    /// <summary>Gets the plan compiled inside the owned scope.</summary>
    /// <value>The activation collaborators. They are invalid after this lease is disposed.</value>
    internal AgentRunPlan Plan { get; }

    /// <summary>Publishes the accepted run identity into this scope.</summary>
    /// <param name="identity">The non-null identity discovered after admission allocated a <see cref="RunId"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="identity"/> is null.</exception>
    /// <remarks>
    /// Collaborators resolved before this call do not observe the identity. The runtime calls this after acceptance
    /// and before the loop is driven, which is the earliest moment a run identity exists.
    /// </remarks>
    internal void BindRunIdentity(RunScopeIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        _provider.GetRequiredService<RunScopeState>().Identity = identity;
    }

    /// <summary>Disposes the owned scope.</summary>
    /// <returns>The scope's asynchronous disposal.</returns>
    public ValueTask DisposeAsync() => _ownedScope.DisposeAsync();
}
