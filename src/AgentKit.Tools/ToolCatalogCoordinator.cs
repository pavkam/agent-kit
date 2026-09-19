// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Chains source discovery, catalog merge, and per-descriptor schema/capability preflight into one owned attempt.</summary>
/// <remarks>
/// This stateless coordinator owns no source capture itself. It acquires a complete <see cref="ToolDiscoveryCapture"/>
/// from <see cref="ToolCatalogDiscovery"/>, resolves one collision decision through <see cref="ToolCatalogMerger"/>, then
/// preflights every merged descriptor's declared schemas through the configured <see cref="IToolSchemaEngine"/> before
/// transferring ownership to a validated <see cref="ToolCatalogCapture"/>. A merge rejection or schema/capability
/// rejection releases every discovered source before returning; only a successful capture keeps the source graph alive,
/// now owned by the returned catalog. This type performs no alias policy, activation lookup, or tool invocation, and it
/// is not yet the public <c>IToolCatalog.CaptureAsync</c> surface (workstream 4, chunk C10b promotes it).
/// </remarks>
internal sealed class ToolCatalogCoordinator
{
    private readonly ToolCatalogDiscovery _discovery;
    private readonly ToolCatalogMerger _merger;
    private readonly IToolSchemaEngine _schemaEngine;
    private readonly ToolSchemaLimits _schemaLimits;
    private readonly IIdentifierGenerator<ToolCatalogVersion> _versions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ToolCatalogCoordinator> _logger;

    /// <summary>Captures every collaborator this coordinator chains without performing discovery, merge, or compilation.</summary>
    /// <param name="discovery">The nonnull owner of complete selected-source acquisition.</param>
    /// <param name="merger">The nonnull collision-policy coordinator for one merge attempt.</param>
    /// <param name="schemaEngine">The nonnull bounded canonical schema compiler used for preflight.</param>
    /// <param name="schemaLimits">The nonnull byte, depth, node, and work bounds applied to every descriptor's schemas.</param>
    /// <param name="versions">The nonnull generator assigning a fresh catalog version to each merge attempt.</param>
    /// <param name="timeProvider">The nonnull observation-only clock.</param>
    /// <param name="logger">The nonnull coordinator-category logger.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    internal ToolCatalogCoordinator(
        ToolCatalogDiscovery discovery,
        ToolCatalogMerger merger,
        IToolSchemaEngine schemaEngine,
        ToolSchemaLimits schemaLimits,
        IIdentifierGenerator<ToolCatalogVersion> versions,
        TimeProvider timeProvider,
        ILogger<ToolCatalogCoordinator> logger)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(merger);
        ArgumentNullException.ThrowIfNull(schemaEngine);
        ArgumentNullException.ThrowIfNull(schemaLimits);
        ArgumentNullException.ThrowIfNull(versions);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _discovery = discovery;
        _merger = merger;
        _schemaEngine = schemaEngine;
        _schemaLimits = schemaLimits;
        _versions = versions;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Discovers, merges, and preflights one complete catalog attempt for the supplied request.</summary>
    /// <param name="request">The nonnull coherent run-bound discovery request.</param>
    /// <param name="cancellationToken">Cancellation checked between stages; already-owned sources are always released before the token's exception propagates.</param>
    /// <returns>A captured catalog, a merge rejection, or a schema/capability rejection; the first two release every discovered source before returning.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException">The caller cancels before a terminal result is produced; every discovered source is released first.</exception>
    /// <remarks>A successful capture's sole owner is the returned <see cref="ToolCatalogCoordinatorCaptured.Catalog"/>; this coordinator retains nothing afterward.</remarks>
    internal async ValueTask<ToolCatalogCoordinatorResult> CaptureAsync(ToolDiscoveryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var observation = new ToolCatalogCoordinationObservation(request, _timeProvider, _logger);
        try
        {
            var discovered = await _discovery.DiscoverAsync(request, cancellationToken).ConfigureAwait(false);
            try
            {
                var result = await CaptureFromDiscoveredAsync(discovered, request, cancellationToken).ConfigureAwait(false);
                observation.Complete(Outcome(result));
                return result;
            }
            catch
            {
                await discovered.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            observation.Complete("cancelled");
            throw;
        }
        catch
        {
            observation.Complete("failed");
            throw;
        }
    }

    private async ValueTask<ToolCatalogCoordinatorResult> CaptureFromDiscoveredAsync(
        ToolDiscoveryCapture discovered, ToolDiscoveryRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(discovered is not null && request is not null, "The caller validates both references before this stage runs.");
        cancellationToken.ThrowIfCancellationRequested();
        var version = _versions.Create();
        var (snapshot, context) = await _merger.MergeAsync(
            request, version, discovered.Selection.Toolsets, discovered.SourceSnapshots, cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            await discovered.DisposeAsync().ConfigureAwait(false);
            return new ToolCatalogCoordinatorMergeRejected(context);
        }

        foreach (var tool in snapshot.Tools)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var inputResult = _schemaEngine.Compile(tool.InputSchema, _schemaLimits, cancellationToken);
            if (inputResult is ToolSchemaCompilationRejected inputRejected)
            {
                await discovered.DisposeAsync().ConfigureAwait(false);
                return new ToolCatalogCoordinatorSchemaRejected(tool, false, inputRejected);
            }

            if (tool.OutputSchema is { } outputSchema)
            {
                var outputResult = _schemaEngine.Compile(outputSchema, _schemaLimits, cancellationToken);
                if (outputResult is ToolSchemaCompilationRejected outputRejected)
                {
                    await discovered.DisposeAsync().ConfigureAwait(false);
                    return new ToolCatalogCoordinatorSchemaRejected(tool, true, outputRejected);
                }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var catalog = discovered.TransferToCatalog(snapshot, cancellationToken);
        return new ToolCatalogCoordinatorCaptured(catalog);
    }

    private static string Outcome(ToolCatalogCoordinatorResult result) => result switch
    {
        ToolCatalogCoordinatorCaptured => "captured",
        ToolCatalogCoordinatorMergeRejected => "merge_rejected",
        ToolCatalogCoordinatorSchemaRejected => "schema_rejected",
        _ => throw new UnreachableException("Coordination produces only the three declared terminal outcomes."),
    };
}
