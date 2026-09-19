// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Owns one captured catalog and activation lease for an engine, agent, run, turn, or operation boundary, and creates a fresh <see cref="HookDispatchContext"/> for every dispatch within it.</summary>
/// <remarks>
/// <para>
/// The hooks architecture describes <see cref="HookDispatchContext"/> as carrying a per-dispatch
/// <see cref="HookDispatchMetadata"/> while also describing one catalog and activation lease as captured once for
/// a broader engine, agent, run, turn, or operation scope. This type reconciles that by being the actual holder of
/// the broader-scoped catalog and lease: a caller creates one scope at the declared capture boundary (typically
/// once per run), then calls <see cref="CreateDispatch"/> with fresh metadata for every individual dispatch that
/// occurs within it, producing dispatch contexts that share the same catalog and lease but never share dispatch
/// identity or timing.
/// </para>
/// <para>
/// This type owns the lease's lifetime: its owner disposes the scope exactly once, at the end of the declared
/// capture boundary, which disposes the underlying <see cref="IHookActivationLease"/> and everything it owns.
/// </para>
/// </remarks>
public sealed class HookActivationScope: IAsyncDisposable
{
    private readonly IHookActivationLease _activation;
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="HookActivationScope"/> class.</summary>
    /// <param name="catalog">The catalog captured for this scope's boundary.</param>
    /// <param name="activation">The activation lease owning this scope's hook instances and reentrancy tracker.</param>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> or <paramref name="activation"/> is null.</exception>
    public HookActivationScope(HookCatalogSnapshot catalog, IHookActivationLease activation)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(activation);

        Catalog = catalog;
        _activation = activation;
    }

    /// <summary>Gets the catalog captured for this scope's boundary.</summary>
    public HookCatalogSnapshot Catalog { get; }

    /// <summary>Creates a dispatch context for one individual dispatch within this scope, sharing this scope's catalog and activation lease.</summary>
    /// <param name="dispatch">This individual emission's point, dispatch, causal, and timing facts.</param>
    /// <returns>A dispatch context ready to pass to <see cref="IHookDispatcher"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dispatch"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">This scope has already been disposed.</exception>
    public HookDispatchContext CreateDispatch(HookDispatchMetadata dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        return new HookDispatchContext(Catalog, dispatch, _activation);
    }

    /// <summary>Disposes the underlying activation lease exactly once, releasing every hook instance and the reentrancy tracker it owns.</summary>
    /// <returns>A task that completes when the lease has finished disposing.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _activation.DisposeAsync().ConfigureAwait(false);
    }
}
