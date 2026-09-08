// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Represents an immutable process-level AgentKit composition that hosts a
/// versioned catalog of agent definitions and runs them concurrently.
/// </summary>
/// <remarks>
/// <para>
/// The engine is not an agent. It carries no agent's mutable state and never
/// makes a process-wide singleton out of a run scope. Each invocation creates
/// an isolated dependency-injection scope and a fresh <see cref="RunId"/>.
/// </para>
/// <para>
/// An engine created by <see cref="AgentEngineBuilder.Build"/> owns the
/// service provider captured by that build and disposes it exactly once. An
/// engine resolved from a host-owned provider does not own or dispose that
/// provider.
/// </para>
/// <para>
/// The container is never exposed. Callers resolve agents by
/// <see cref="AgentId"/> and run them through the returned
/// <see cref="Agent"/> handle, so no caller can bypass definition resolution
/// or substitute a component the composition did not validate.
/// </para>
/// </remarks>
public sealed class AgentEngine: IAsyncDisposable
{
    private readonly Lock _disposeLock = new();
    private readonly IServiceProvider _services;
    private readonly IAsyncDisposable? _ownedProvider;
    private readonly IAgentDefinitionCatalog _catalog;
    private readonly IIdentifierGenerator<RunId> _runIds;
    private readonly IIdentifierGenerator<OperationId> _operationIds;
    private readonly IAgentRunProfilePublicationReader _runProfiles;
    private readonly ISecurityProfileSelector _securityProfiles;
    private readonly ImmutableDictionary<(AgentId, AgentDefinitionRevision), AgentRunProfilePublication> _pinnedRunProfiles;
    private readonly ILogger<AgentEngine> _logger;
    private Task? _disposeTask;

    /// <summary>
    /// Initializes an engine over one captured composition.
    /// </summary>
    /// <param name="services">
    /// The composition the engine resolves scoped run services from.
    /// </param>
    /// <param name="ownedProvider">
    /// The standalone provider owned by this engine, or <see langword="null"/>
    /// when an external host owns the provider.
    /// </param>
    /// <param name="validatedRunProfiles">
    /// The exact immutable snapshot already inspected by composition validation. Engine
    /// construction pins this supplied value without consulting the replaceable reader again.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="validatedRunProfiles"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The composition does not contain the engine-wide services the facade
    /// requires.
    /// </exception>
    internal AgentEngine(
        IServiceProvider services,
        IAsyncDisposable? ownedProvider,
        AgentRunProfilePublicationSnapshot validatedRunProfiles)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(validatedRunProfiles);

        _services = services;
        _ownedProvider = ownedProvider;
        TimeProvider = services.GetRequiredService<TimeProvider>();
        _catalog = services.GetRequiredService<IAgentDefinitionCatalog>();
        _runIds = services.GetRequiredService<IIdentifierGenerator<RunId>>();
        _operationIds = services.GetRequiredService<IIdentifierGenerator<OperationId>>();
        _runProfiles = services.GetRequiredService<IAgentRunProfilePublicationReader>();
        _securityProfiles = services.GetRequiredService<ISecurityProfileSelector>();
        _pinnedRunProfiles = validatedRunProfiles.Publications.ToImmutableDictionary(
            static publication => (
                publication.SecurityProfile.AgentId,
                publication.SecurityProfile.AgentDefinitionRevision));
        _logger = services.GetService<ILogger<AgentEngine>>() ?? NullLogger<AgentEngine>.Instance;
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
    /// Lists every agent this engine currently hosts.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>
    /// The immutable definitions in the current catalog snapshot, in
    /// composition order.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was signalled.
    /// </exception>
    public async ValueTask<ImmutableArray<AgentDefinition>> GetAgentsAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _catalog.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return snapshot.Definitions;
    }

    /// <summary>
    /// Resolves one hosted agent to a runnable handle.
    /// </summary>
    /// <param name="agentId">The agent identity to resolve.</param>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>
    /// A handle over the resolved definition, or <see langword="null"/> when
    /// this engine hosts no such agent.
    /// </returns>
    /// <remarks>
    /// An unknown identity returns <see langword="null"/> rather than
    /// throwing, because agent identities routinely arrive from outside the
    /// process and a host should be able to answer "no such agent" without
    /// catching.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The agent exists but its definition cannot be used with this
    /// composition.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was signalled.
    /// </exception>
    public async ValueTask<Agent?> GetAgentAsync(
        AgentId agentId,
        CancellationToken cancellationToken = default)
    {
        var resolution = await _catalog.ResolveAsync(agentId, cancellationToken).ConfigureAwait(false);

        return resolution switch
        {
            ResolvedAgentDefinition resolved =>
                new Agent(this, resolved.Definition, resolved.CatalogVersion),
            AgentDefinitionNotFound => null,
            InvalidAgentDefinition invalid => throw new InvalidOperationException(
                $"Agent '{agentId}' is hosted but its definition is unusable: "
                + string.Join("; ", invalid.Diagnostics)),
            _ => throw new InvalidOperationException(
                $"Unrecognized {nameof(AgentDefinitionResolution)} kind '{resolution.GetType()}'."),
        };
    }

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

    /// <summary>
    /// Runs one agent in a fresh, isolated run scope.
    /// </summary>
    /// <param name="definition">The immutable definition to run.</param>
    /// <param name="options">The per-invocation facts and bounded overrides.</param>
    /// <param name="cancellationToken">A token that cancels the run.</param>
    /// <returns>The loop's terminal result.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An override in <paramref name="options"/> is wider than the
    /// definition's corresponding default.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="definition"/> or <paramref name="options"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="AgentAdmissionRejectedException">
    /// The current catalog does not enable <paramref name="definition"/>'s
    /// exact pinned content and revision. The exception is raised before a
    /// run identity or scope is created.
    /// </exception>
    internal async Task<AgentLoopResult> RunAgentAsync(
        AgentDefinition definition,
        AgentRunOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(options);
        var activity = AgentAdmissionObservability.Start(definition.Id);
        var admissionCompleted = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await _catalog.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
            _ = activity?.SetTag(AgentKitTagNames.AgentCatalogVersion, snapshot.Version.ToString());
            var current = snapshot.FindDefinition(definition.Id);
            if (current is null || current.Revision != definition.Revision || !current.Equals(definition))
            {
                var rejection = new AgentAdmissionRejectedException(new AgentAdmissionRejection(definition.Id, definition.Revision, snapshot.Version, current is null ? "The pinned agent definition is no longer enabled for new admission." : "The pinned agent definition was replaced and cannot be silently upgraded."));
                AgentAdmissionObservability.Complete(activity, _logger, "rejected", rejection.GetType().Name);
                activity = null;
                admissionCompleted = true;
                throw rejection;
            }

            var maxTurns = ResolveMaxTurns(definition, options);
            var attemptTimeout = ResolveAttemptTimeout(definition, options);

            if (!_pinnedRunProfiles.TryGetValue((definition.Id, definition.Revision), out var pinnedPublication))
            {
                throw AdmissionRejected(
                    definition, snapshot.Version,
                    "The built composition has no pinned run-profile publication for this definition.");
            }

            var publicationResult = await _runProfiles.ReadAsync(
                definition.Id, definition.Revision, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (publicationResult is not AgentRunProfilePublicationFound found
                || found.Publication != pinnedPublication)
            {
                throw AdmissionRejected(
                    definition, snapshot.Version,
                    "The exact run-profile publication changed after composition validation.");
            }

            var runId = _runIds.Create();
            var runCorrelation = new InRunOperationCorrelation(_operationIds.Create(), runId, turnId: null);
            var security = pinnedPublication.SecurityProfile;
            var authorizationResult = await _securityProfiles.SelectAsync(
                new SecurityAuthorizationCaptureRequest(
                    new SecurityAuthorizationScope(definition.Id, options.SessionId, runCorrelation),
                    security.ProfileKey,
                    security.AgentDefinitionRevision,
                    security.ConfigurationVersion,
                    options.Identity),
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (authorizationResult is not SecurityAuthorizationCaptured captured
                || !Matches(captured.Authorization, security, runCorrelation, options))
            {
                throw AdmissionRejected(
                    definition, snapshot.Version,
                    "Fresh run-start authorization did not match the pinned security publication.");
            }

            await using var scope = _services.CreateAsyncScope();
            var loop = scope.ServiceProvider.GetRequiredService<IAgentLoop>();

            var request = new AgentRunRequest(
                definition.Id,
                options.SessionId,
                options.BranchId,
                runId,
                options.Identity,
                captured.Authorization,
                pinnedPublication.SessionProfile,
                definition.Models,
                definition.ModelRequirements,
                definition.Instructions,
                definition.Tools,
                definition.ToolChoice,
                definition.Settings,
                maxTurns,
                attemptTimeout,
                definition.Extensions);

            AgentAdmissionObservability.Complete(activity, _logger, "admitted");
            activity = null;
            admissionCompleted = true;
            return await loop.RunAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!admissionCompleted && cancellationToken.IsCancellationRequested)
        {
            AgentAdmissionObservability.Complete(activity, _logger, "cancelled", nameof(OperationCanceledException));
            AgentAdmissionObservability.LogCancelled(_logger);
            throw;
        }
        catch (AgentAdmissionRejectedException) when (!admissionCompleted)
        {
            AgentAdmissionObservability.Complete(
                activity, _logger, "rejected", nameof(AgentAdmissionRejectedException));
            activity = null;
            admissionCompleted = true;
            throw;
        }
        catch (Exception exception) when (!admissionCompleted)
        {
            AgentAdmissionObservability.Complete(activity, _logger, "failed", exception.GetType().Name);
            AgentAdmissionObservability.LogFailed(_logger, exception.GetType().Name);
            throw;
        }
    }

    private static AgentAdmissionRejectedException AdmissionRejected(
        AgentDefinition definition,
        AgentCatalogVersion catalogVersion,
        string reason) => new(new AgentAdmissionRejection(definition.Id, definition.Revision, catalogVersion, reason));

    private static bool Matches(
        SecurityAuthorizationContext authorization,
        SecurityProfilePublication publication,
        InRunOperationCorrelation correlation,
        AgentRunOptions options)
    {
        Debug.Assert(authorization is not null, "Captured authorization is required for evidence matching.");
        Debug.Assert(publication is not null, "Pinned security publication is required for evidence matching.");
        Debug.Assert(options is not null, "Validated run options are required for evidence matching.");

        return authorization.Scope.AgentId == publication.AgentId
            && authorization.Scope.SessionId == options.SessionId
            && authorization.Scope.Correlation == correlation
            && authorization.Identity == options.Identity
            && authorization.ProfileKey == publication.ProfileKey
            && authorization.ProfileVersion == publication.ProfileVersion
            && authorization.PolicySnapshot == publication.PolicySnapshot
            && authorization.AuthorityKey == publication.AuthorityKey
            && authorization.AgentDefinitionRevision == publication.AgentDefinitionRevision
            && authorization.ConfigurationVersion == publication.ConfigurationVersion;
    }

    private static int ResolveMaxTurns(AgentDefinition definition, AgentRunOptions options) =>
        options.MaxTurns is not { } requested
            ? definition.RunDefaults.MaxTurns
            : requested <= definition.RunDefaults.MaxTurns
                ? requested
                : throw new ArgumentOutOfRangeException(
                    nameof(options),
                    requested,
                    "A run override may only narrow the definition's turn limit of "
                    + $"{definition.RunDefaults.MaxTurns}.");

    private static TimeSpan ResolveAttemptTimeout(
        AgentDefinition definition,
        AgentRunOptions options) =>
        options.AttemptTimeout is not { } requested
            ? definition.RunDefaults.AttemptTimeout
            : requested <= definition.RunDefaults.AttemptTimeout
                ? requested
                : throw new ArgumentOutOfRangeException(
                    nameof(options),
                    requested,
                    "A run override may only narrow the definition's attempt timeout of "
                    + $"{definition.RunDefaults.AttemptTimeout}.");

    private static async Task DisposeOwnedProviderAsync(IAsyncDisposable? ownedProvider)
    {
        if (ownedProvider is not null)
        {
            await ownedProvider.DisposeAsync().ConfigureAwait(false);
        }
    }
}
