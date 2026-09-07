// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Represents an immutable process-level AgentKit composition.
/// </summary>
/// <remarks>
/// An engine created by <see cref="AgentEngineBuilder.Build"/> owns the service
/// provider captured by that build and disposes it exactly once. An engine
/// resolved from a host-owned service provider does not own or dispose that
/// provider. Runnable agent operations will be added when their neutral
/// catalog and run-scope contracts are available; this facade does not expose
/// the dependency-injection container as a service locator.
/// </remarks>
public sealed class AgentEngine: IAsyncDisposable
{
    private readonly Lock _disposeLock = new();
    private readonly IAsyncDisposable? _ownedProvider;
    private Task? _disposeTask;

    /// <summary>
    /// Initializes an engine over one captured foundation composition.
    /// </summary>
    /// <param name="timeProvider">
    /// The engine-wide time provider captured when the engine is created.
    /// </param>
    /// <param name="ownedProvider">
    /// The standalone provider owned by this engine, or <see langword="null"/>
    /// when an external host owns the provider.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="timeProvider"/> is <see langword="null"/>.
    /// </exception>
    internal AgentEngine(TimeProvider timeProvider, IAsyncDisposable? ownedProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        TimeProvider = timeProvider;
        _ownedProvider = ownedProvider;
    }

    /// <summary>
    /// Gets the engine-wide clock captured at composition time.
    /// </summary>
    /// <value>
    /// The singleton <see cref="System.TimeProvider"/> selected by the
    /// standalone builder or external host. The reference never changes for
    /// this engine.
    /// </value>
    internal TimeProvider TimeProvider { get; }

    /// <summary>
    /// Creates a mutable builder for a new standalone engine composition.
    /// </summary>
    /// <returns>
    /// A new builder with an independent service collection and the AgentKit
    /// facade defaults registered.
    /// </returns>
    public static AgentEngineBuilder CreateBuilder() => new();

    /// <summary>
    /// Releases the standalone service provider owned by this engine.
    /// </summary>
    /// <returns>
    /// An operation that completes when owned services have finished
    /// asynchronous disposal. Hosted engines complete without disposing any
    /// host-owned service.
    /// </returns>
    /// <remarks>
    /// Disposal is thread-safe and idempotent. Concurrent and subsequent calls
    /// observe the same disposal operation, so the owned provider is disposed
    /// at most once.
    /// </remarks>
    public ValueTask DisposeAsync()
    {
        lock (_disposeLock)
        {
            _disposeTask ??= DisposeOwnedProviderAsync(_ownedProvider);
            return new ValueTask(_disposeTask);
        }
    }

    private static async Task DisposeOwnedProviderAsync(IAsyncDisposable? ownedProvider)
    {
        if (ownedProvider is not null)
        {
            await ownedProvider.DisposeAsync().ConfigureAwait(false);
        }
    }
}
