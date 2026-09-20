// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Represents an immutable process-level AgentKit composition that hosts a versioned catalog of agent definitions
/// and runs them concurrently.
/// </summary>
/// <remarks>
/// <para>
/// The engine is not an agent. It carries no agent's mutable state. Admission, scope ownership, and disposal live
/// on the package-internal <see cref="AgentEngineRuntime"/> this type delegates to. Callers never receive that
/// runtime or a service provider from an <see cref="Agent"/> handle.
/// </para>
/// <para>
/// An engine created by <see cref="AgentEngineBuilder.Build"/> owns the service provider captured by that build
/// and disposes it exactly once. An engine resolved from a host-owned provider does not own or dispose that
/// provider.
/// </para>
/// </remarks>
public sealed class AgentEngine: IAsyncDisposable
{
    private readonly AgentEngineRuntime _runtime;

    /// <summary>Initializes an engine over one runtime.</summary>
    /// <param name="runtime">The non-null lifecycle coordinator. The engine does not construct a second one.</param>
    /// <exception cref="ArgumentNullException"><paramref name="runtime"/> is null.</exception>
    internal AgentEngine(AgentEngineRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
    }

    /// <summary>
    /// Gets the composed service provider so the application that built this engine can reach the services it
    /// registered, in the same way an <c>IHost</c> exposes its services.
    /// </summary>
    /// <remarks>
    /// This is a composition-root surface for the application only. Runtime components never receive it: every
    /// framework collaborator is injected through its constructor. The provider's lifetime is the engine's; for a
    /// standalone engine it is disposed with the engine, and for a host-managed engine it is the host's provider.
    /// </remarks>
    public IServiceProvider Services => _runtime.Services;

    /// <summary>Gets the engine-wide clock captured at composition time.</summary>
    internal TimeProvider TimeProvider => _runtime.TimeProvider;

    /// <summary>Gets the exact partial component-registration evidence validated for this engine.</summary>
    internal ComponentRegistrationSnapshot ComponentRegistrations => _runtime.ComponentRegistrations;

    /// <summary>Creates a mutable builder for a new standalone engine composition.</summary>
    /// <returns>A new builder with an independent service collection and the AgentKit facade defaults registered.</returns>
    public static AgentEngineBuilder CreateBuilder() => new();

    /// <summary>Lists every agent this engine currently hosts.</summary>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>The current catalog snapshot, including its version and definitions in composition order.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    /// <exception cref="ObjectDisposedException">This engine has already been disposed.</exception>
    public ValueTask<AgentCatalogSnapshot> GetAgentsAsync(CancellationToken cancellationToken = default) =>
        _runtime.GetAgentsAsync(cancellationToken);

    /// <summary>Resolves one hosted agent to a runnable handle.</summary>
    /// <param name="agentId">The agent identity to resolve.</param>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>
    /// <see cref="ResolvedAgent"/> wrapping a handle over the resolved definition, <see cref="AgentNotFound"/> when
    /// this engine hosts no such agent, or <see cref="InvalidAgent"/> when the agent exists but its definition
    /// cannot be used with this composition.
    /// </returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    /// <exception cref="ObjectDisposedException">This engine has already been disposed.</exception>
    public ValueTask<AgentResolution> GetAgentAsync(AgentId agentId, CancellationToken cancellationToken = default) =>
        _runtime.GetAgentAsync(agentId, cancellationToken);

    /// <summary>Creates a new session for one agent without admitting any turn.</summary>
    /// <param name="request">The agent, identity, conversation, idempotency key, and extensions for the new session.</param>
    /// <param name="cancellationToken">A token that cancels the creation.</param>
    /// <returns>
    /// <see cref="AgentSessionCreated"/> naming the new or existing session, or <see cref="AgentSessionCreationFailed"/>
    /// with safe, closed evidence.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">This engine has been disposed.</exception>
    public Task<AgentSessionCreationResult> CreateSessionAsync(
        AgentSessionCreateRequest request,
        CancellationToken cancellationToken = default) =>
        _runtime.CreateSessionAsync(request, cancellationToken);

    /// <summary>Runs one request to settlement.</summary>
    /// <typeparam name="TOutput">The requested output type.</typeparam>
    /// <param name="request">The agent, session, identity, and input.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The finished envelope, or a rejection that carries no run identity.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">This engine has been disposed.</exception>
    public Task<AgentRunResult<TOutput>> RunAsync<TOutput>(
        AgentRunRequest request,
        CancellationToken cancellationToken = default) =>
        _runtime.RunAsync<TOutput>(request, cancellationToken);

    /// <summary>Subscribes to one request's run before the loop is driven.</summary>
    /// <typeparam name="TOutput">The requested output type.</typeparam>
    /// <param name="request">The agent, session, identity, and input.</param>
    /// <param name="cancellationToken">Cancels admission and the drive. Disposing the stream does not.</param>
    /// <returns>A started stream, or a rejection with no subscription and no run identity.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">This engine has been disposed.</exception>
    public Task<AgentRunStreamStartResult<TOutput>> StreamAsync<TOutput>(
        AgentRunRequest request,
        CancellationToken cancellationToken = default) =>
        _runtime.StreamAsync<TOutput>(request, cancellationToken);

    /// <summary>Releases the standalone service provider owned by this engine.</summary>
    /// <returns>
    /// An operation that completes when owned services have finished asynchronous disposal. Hosted engines complete
    /// without disposing any host-owned service.
    /// </returns>
    /// <remarks>Disposal is thread-safe and idempotent. Concurrent calls observe the same disposal operation.</remarks>
    public ValueTask DisposeAsync() => _runtime.DisposeAsync();
}
