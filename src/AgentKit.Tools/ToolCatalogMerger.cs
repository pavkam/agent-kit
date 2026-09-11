// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Coordinates one complete immutable catalog merge through the replaceable collision policy.</summary>
/// <remarks>This stateless service owns no source capture. Its caller retains and cleans up all discovered sources, including on rejection, cancellation, or policy failure. Schema/capability preflight remains with catalog capture.</remarks>
internal sealed class ToolCatalogMerger
{
    private readonly IToolCatalogMergePolicy _policy;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ToolCatalogMerger> _logger;

    /// <summary>Captures the concurrently callable policy and isolated diagnostics dependencies.</summary>
    /// <param name="policy">The nonnull explicit merge policy.</param>
    /// <param name="timeProvider">The nonnull clock used only for diagnostic duration.</param>
    /// <param name="logger">The nonnull type-specific safe logger.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public ToolCatalogMerger(IToolCatalogMergePolicy policy, TimeProvider timeProvider, ILogger<ToolCatalogMerger> logger)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _policy = policy;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Validates all publications, calls policy once, and validates its complete selection before snapshot construction.</summary>
    /// <param name="request">The nonnull coherent discovery request.</param>
    /// <param name="version">The nondefault catalog version assigned by the owner.</param>
    /// <param name="toolsets">Initialized publications matching authored toolsets in request order.</param>
    /// <param name="sources">The nonnull complete source publication map, including selected empty sources.</param>
    /// <param name="cancellationToken">Caller cancellation checked before policy and again before snapshot transfer.</param>
    /// <returns>The complete context and either a validated snapshot or null for policy rejection.</returns>
    /// <exception cref="ArgumentNullException">A required reference or a source-map value is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A required identity or version is default.</exception>
    /// <exception cref="ArgumentException">The toolset array is default or contains null, publication membership differs from the request or source evidence is malformed.</exception>
    /// <exception cref="InvalidOperationException">Policy returns null, fabricated, incomplete, or incoherent selection evidence.</exception>
    /// <exception cref="OperationCanceledException">The operation is cancelled before transfer.</exception>
    /// <remarks>Policy exceptions propagate after isolated failure diagnostics. No partial graph is advertised and this method never acquires, invokes, or disposes a tool.</remarks>
    internal async ValueTask<(ToolCatalogSnapshot? Snapshot, ToolCatalogMergeContext Context)> MergeAsync(
        ToolDiscoveryRequest request, ToolCatalogVersion version, ImmutableArray<ToolsetPublication> toolsets,
        ImmutableDictionary<ToolSourceId, ToolProviderSnapshot> sources, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfEqual(version, default);
        var graph = new ToolCatalogMergeGraph(request, toolsets, sources);
        using var observation = new ToolCatalogMergeObservation(false, request, _timeProvider, _logger);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var decision = await _policy.ResolveAsync(graph.Context, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (decision is ToolCatalogRejection)
            {
                observation.Complete("rejected");
                return (null, graph.Context);
            }
            if (decision is not ToolCatalogSelection selection)
            {
                throw new InvalidOperationException("Catalog policy must return a defined nonnull decision.");
            }
            var snapshot = graph.Apply(selection, version);
            cancellationToken.ThrowIfCancellationRequested();
            observation.Complete("selected");
            return (snapshot, graph.Context);
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
}
