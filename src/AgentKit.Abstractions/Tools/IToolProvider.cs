// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Discovers one selected source publication and transfers an owned capture of its exact bindings.</summary>
/// <remarks>
/// Providers are additive sources, not catalogs, alias resolvers, or invokers. The catalog selects
/// sources from toolset publications and contacts each selected source once per capture. A provider
/// may filter availability using the supplied identity and captured policy evidence; protected or
/// remote discovery must obtain and enforce its own grant before effects. Request evidence alone
/// grants no authority. Implementations support concurrent requests without retaining mutable run state.
/// </remarks>
public interface IToolProvider
{
    /// <summary>Gets this provider's stable composition identity without discovery or external I/O.</summary>
    /// <value>The nondefault source identity, stable for the provider's lifetime and shared by every returned snapshot.</value>
    public ToolSourceId SourceId { get; }

    /// <summary>Captures this source's current publication and the lifetime needed to acquire its exact invokers.</summary>
    /// <param name="request">The nonnull immutable, locally coherent discovery context supplied by the catalog.</param>
    /// <param name="cancellationToken">Cancels discovery before ownership transfers; it does not close an already returned capture.</param>
    /// <returns>A nonnull independently owned capture whose snapshot belongs to <see cref="SourceId"/>, including a valid empty publication.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before ownership transfer.</exception>
    /// <remarks>
    /// Success transfers one acquisition to the caller; closing a previous capture must not close a
    /// later one. Failure or cancellation releases partial acquisitions before propagating and exposes
    /// no partial publication. Cleanup failures remain observable. Returned captures preserve their
    /// original source versions through later refreshes. Borrowed host resources retain their owner.
    /// The catalog still validates source identity, merge collisions, aliases, schemas, and capabilities
    /// before exposing tools to a model. Discovery never invokes a tool or grants execution authority.
    /// </remarks>
    public ValueTask<IToolProviderCapture> DiscoverAsync(ToolDiscoveryRequest request, CancellationToken cancellationToken = default);
}
