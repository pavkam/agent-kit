// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An immutable, engine-bound handle over one validated agent definition.
/// </summary>
/// <remarks>
/// <para>
/// An <see cref="Agent"/> owns no mutable session or run state. It is safe to
/// cache and to use concurrently: every invocation creates its own isolated
/// run scope, so two callers running the same agent never share anything but
/// the immutable definition.
/// </para>
/// <para>
/// The handle deliberately exposes no service provider. Callers address work
/// through <see cref="RunAsync"/>; they cannot reach into the container,
/// bypass definition validation, or substitute a different loop.
/// </para>
/// <para>
/// The handle captures the definition and catalog version it was resolved
/// with. A later catalog reload does not retroactively change an existing
/// handle's behavior; a caller that wants newer configuration resolves the
/// agent again.
/// </para>
/// </remarks>
public sealed class Agent
{
    private readonly AgentEngine _engine;

    /// <summary>
    /// Initializes a handle over one resolved definition.
    /// </summary>
    /// <param name="engine">The engine that owns this handle's composition.</param>
    /// <param name="definition">The validated definition this handle runs.</param>
    /// <param name="catalogVersion">
    /// The catalog revision the definition was resolved from.
    /// </param>
    internal Agent(AgentEngine engine, AgentDefinition definition, AgentCatalogVersion catalogVersion)
    {
        Debug.Assert(engine is not null, "A resolved handle always has an owning engine.");
        Debug.Assert(definition is not null, "A resolved handle always has a validated definition.");

        _engine = engine;
        Definition = definition;
        CatalogVersion = catalogVersion;
    }

    /// <summary>Gets this agent's stable identity.</summary>
    public AgentId Id => Definition.Id;

    /// <summary>Gets the immutable definition this handle runs.</summary>
    public AgentDefinition Definition { get; }

    /// <summary>
    /// Gets the catalog revision this handle's definition was resolved from.
    /// </summary>
    public AgentCatalogVersion CatalogVersion { get; }

    /// <summary>
    /// Runs this agent once in its own isolated run scope.
    /// </summary>
    /// <param name="options">
    /// The session, branch, identity, and bounded overrides for this
    /// invocation.
    /// </param>
    /// <param name="cancellationToken">A token that cancels the run.</param>
    /// <returns>
    /// The loop's terminal result, including the committed messages and final
    /// session version.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Each call creates a new dependency-injection scope and a new
    /// <see cref="RunId"/>, resolves the selected loop from that scope, and
    /// disposes the scope when the run settles. No mutable run state is
    /// retained on this handle or on the engine.
    /// </para>
    /// <para>
    /// Overrides in <paramref name="options"/> may only narrow the
    /// definition's limits; an attempt to widen them is rejected before the
    /// run starts.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An override in <paramref name="options"/> is wider than the
    /// definition's corresponding default.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The owning engine has been disposed.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was signalled.
    /// </exception>
    public Task<AgentLoopResult> RunAsync(
        AgentRunOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return _engine.RunAgentAsync(Definition, options, cancellationToken);
    }
}
