// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Acquires every explicitly selected source before catalog merge and schema preflight.</summary>
/// <remarks>This stateless coordinator owns partial acquisitions until a complete discovery capture transfers. It selects no aliases, invokes no tools, performs no container lookup, and never exposes a model-ready catalog.</remarks>
internal sealed class ToolCatalogDiscovery
{
    private readonly IToolRegistrationCatalog _registrations;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ToolCatalogDiscovery> _logger;
    private readonly ILogger<ToolDiscoveryCapture> _captureLogger;
    private readonly ILogger<ToolCatalogCapture> _catalogLogger;

    /// <summary>Captures the materialized registration view and observation dependencies without discovery.</summary>
    /// <param name="registrations">The nonnull concurrently callable registration view.</param>
    /// <param name="timeProvider">The nonnull observation-only clock.</param>
    /// <param name="logger">The nonnull coordinator logger.</param>
    /// <param name="captureLogger">The nonnull discovery-owner logger.</param>
    /// <param name="catalogLogger">The nonnull eventual catalog-owner logger.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    internal ToolCatalogDiscovery(IToolRegistrationCatalog registrations, TimeProvider timeProvider,
        ILogger<ToolCatalogDiscovery> logger, ILogger<ToolDiscoveryCapture> captureLogger, ILogger<ToolCatalogCapture> catalogLogger)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(captureLogger);
        ArgumentNullException.ThrowIfNull(catalogLogger);
        _registrations = registrations;
        _timeProvider = timeProvider;
        _logger = logger;
        _captureLogger = captureLogger;
        _catalogLogger = catalogLogger;
    }

    /// <summary>Resolves the complete selection and discovers each source once in authored first-use order.</summary>
    /// <param name="request">The nonnull coherent run request retained without substitution.</param>
    /// <param name="cancellationToken">Cancellation checked around selection, provider callbacks, and ownership transfer.</param>
    /// <returns>A complete owned discovery capture, including empty sources; the caller must dispose it or transfer it after merge and preflight.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Selection substitutes request evidence, a provider changes source identity, or a returned capture is null, reused, or publishes another source.</exception>
    /// <exception cref="OperationCanceledException">The caller cancels before transfer and cleanup succeeds.</exception>
    /// <exception cref="AggregateException">Discovery fails or is cancelled and one or more owned cleanups also fail; the original failure is first, followed by cleanup failures in ordinal source-ID order.</exception>
    /// <remarks>Provider and metadata failures propagate unchanged when cleanup succeeds. Every returned owner is retained before cancellation or metadata validation. All cleanups start before any is awaited, and cleanup is never cancelled by the discovery token. Providers keep their original disposal owner.</remarks>
    internal async ValueTask<ToolDiscoveryCapture> DiscoverAsync(ToolDiscoveryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var observation = new ToolDiscoveryObservation(AgentKitActivityNames.ToolCatalogDiscover, request, _timeProvider, _logger);
        List<KeyValuePair<ToolSourceId, IToolProviderCapture>> owned = [];
        var identities = new HashSet<IToolProviderCapture>(ReferenceEqualityComparer.Instance);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var selection = _registrations.ResolveSelection(request, cancellationToken);
            if (selection is null || selection.Request != request)
            {
                throw new InvalidOperationException("Tool registration selection substituted the discovery request.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            var snapshots = ImmutableDictionary.CreateBuilder<ToolSourceId, ToolProviderSnapshot>();
            foreach (var binding in selection.Providers)
            {
                using var sourceObservation = new ToolDiscoveryObservation(AgentKitActivityNames.ToolCatalogDiscoverSource, request, _timeProvider, _logger, binding.SourceId);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (binding.Provider.SourceId != binding.SourceId)
                    {
                        throw new InvalidOperationException("The discovery provider changed its registered source identity.");
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    var capture = await binding.Provider.DiscoverAsync(request, cancellationToken).ConfigureAwait(false)
                        ?? throw new InvalidOperationException("The discovery provider returned no source capture.");
                    if (!identities.Add(capture))
                    {
                        throw new InvalidOperationException("Discovery providers returned the same capture for different sources.");
                    }
                    owned.Add(new(binding.SourceId, capture));
                    cancellationToken.ThrowIfCancellationRequested();
                    var snapshot = capture.Snapshot ?? throw new InvalidOperationException("The discovery capture returned no publication.");
                    if (snapshot.SourceId != binding.SourceId)
                    {
                        throw new InvalidOperationException("The discovery capture published a different source identity.");
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    snapshots.Add(binding.SourceId, snapshot);
                    sourceObservation.Complete("discovered");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    sourceObservation.Complete("cancelled");
                    throw;
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            var result = new ToolDiscoveryCapture(selection, snapshots.ToImmutable(), owned.ToImmutableDictionary(), _timeProvider, _captureLogger, _catalogLogger);
            observation.Complete("discovered");
            return result;
        }
        catch (Exception failure)
        {
            var cleanups = await ToolDiscoveryCapture.ReleaseSourcesAsync(request, [.. owned], _timeProvider, _logger).ConfigureAwait(false);
            if (!cleanups.IsEmpty)
            {
                observation.Complete("failed");
                throw new AggregateException("Tool discovery and source cleanup failed.", [failure, .. cleanups]);
            }
            observation.Complete(failure is OperationCanceledException && cancellationToken.IsCancellationRequested ? "cancelled" : "failed");
            throw;
        }
    }
}
