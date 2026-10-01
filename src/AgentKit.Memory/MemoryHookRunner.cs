// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

using System.Diagnostics.CodeAnalysis;

/// <summary>Dispatches the memory hook points through the typed hook kernel under the caller's captured hook context.</summary>
/// <remarks>
/// <para>
/// A memory operation receives one <see cref="HookDispatchContext"/> from its caller. The runner derives a fresh dispatch for
/// each memory point from that context's catalog, activation, correlation, and deadline, so it never selects a live or
/// unkeyed hook profile, never extends the caller's deadline, and persists nothing. A point is dispatched only when the
/// captured catalog contains a registration for it; with no context, no dispatcher, or no registration, the operation runs
/// exactly as it does without hooks.
/// </para>
/// <para>
/// Hook failure is fail-closed. Any exception other than cancellation, an invalid mutation, an unsatisfiable derived deadline,
/// or a timeout surfaces as <c>MemoryHookOutcome.Failed</c>, which callers turn into a typed refusal of the operation;
/// cancellation always propagates. The runner holds no per-operation state and is safe for concurrent use.
/// </para>
/// </remarks>
internal sealed class MemoryHookRunner
{
    /// <summary>The fixed safe message for an operation refused because a memory hook failed.</summary>
    internal const string FailedMessage = "A memory hook failed, so the operation was refused.";

    private readonly IHookDispatcher? _dispatcher;
    private readonly IIdentifierGenerator<HookDispatchId>? _ids;
    private readonly TimeProvider _time;

    /// <summary>Initializes the runner.</summary>
    /// <param name="time">The clock that stamps each derived dispatch.</param>
    /// <param name="dispatcher">The typed hook dispatch kernel, or <see langword="null"/> when no hooks are composed.</param>
    /// <param name="ids">The dispatch identity source, or <see langword="null"/> when no hooks are composed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="time"/> is null.</exception>
    public MemoryHookRunner(TimeProvider time, IHookDispatcher? dispatcher = null, IIdentifierGenerator<HookDispatchId>? ids = null)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
        _dispatcher = dispatcher;
        _ids = ids;
    }

    /// <summary>Determines whether <paramref name="point"/> would dispatch under <paramref name="hooks"/>.</summary>
    /// <param name="hooks">The caller's hook context, or null for a maintenance caller.</param>
    /// <param name="point">The memory hook point.</param>
    /// <returns><see langword="true"/> only when a dispatcher is composed and the captured catalog registers the point.</returns>
    public bool IsActive([NotNullWhen(true)] HookDispatchContext? hooks, HookPointId point) =>
        hooks is not null
        && _dispatcher is not null
        && _ids is not null
        && hooks.Catalog.Registrations.Any(registration => registration.Point.Equals(point));

    /// <summary>Dispatches one memory hook point and reports whether a hook failed the operation.</summary>
    /// <typeparam name="THook">The hook interface of the point.</typeparam>
    /// <typeparam name="TEventArgs">The event arguments of the point.</typeparam>
    /// <param name="hooks">The caller's hook context.</param>
    /// <param name="definition">The closed point definition.</param>
    /// <param name="create">Creates the event arguments for the derived dispatch.</param>
    /// <param name="cancellationToken">A token that cancels the dispatch; cancellation propagates.</param>
    /// <returns>The completed arguments when no hook failed, or <see cref="MemoryHookOutcome{TEventArgs}.Failed"/>.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async ValueTask<MemoryHookOutcome<TEventArgs>> DispatchAsync<THook, TEventArgs>(
        HookDispatchContext hooks,
        HookPointDefinition<THook, TEventArgs> definition,
        Func<HookDispatchMetadata, TEventArgs> create,
        CancellationToken cancellationToken)
        where THook : class
        where TEventArgs : AgentHookEventArgs
    {
        Debug.Assert(hooks is not null, "Callers check IsActive before dispatching.");
        Debug.Assert(_dispatcher is not null && _ids is not null, "Callers check IsActive before dispatching.");

        try
        {
            var derived = hooks.ForPoint(definition.Id, _ids.Create(), _time.GetUtcNow());
            var args = create(derived.Dispatch);
            await _dispatcher.DispatchAsync(definition, derived, args, HookFailureMode.FailOperation, cancellationToken).ConfigureAwait(false);
            return new MemoryHookOutcome<TEventArgs>(args, failed: false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new MemoryHookOutcome<TEventArgs>(default, failed: true);
        }
    }
}
