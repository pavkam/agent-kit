// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Captures run-bound tool catalogs through discovery when toolsets are authored; otherwise delegates to the legacy catalog.</summary>
public sealed class RunToolCatalogCaptureFactory: IToolRunCatalogCaptureFactory
{
    private readonly IToolCatalog _legacyCatalog;
    private readonly ToolCatalogCoordinator _coordinator;

    /// <summary>Initializes the run-bound capture factory.</summary>
    /// <param name="legacyCatalog">The legacy singleton catalog used when no toolsets are supplied.</param>
    /// <param name="coordinator">The internal coordinator that discovers, merges, and preflights toolsets.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public RunToolCatalogCaptureFactory(IToolCatalog legacyCatalog, ToolCatalogCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(legacyCatalog);
        ArgumentNullException.ThrowIfNull(coordinator);
        _legacyCatalog = legacyCatalog;
        _coordinator = coordinator;
    }

    /// <inheritdoc/>
    public IToolCatalogCapture Create(RunToolCatalogCaptureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Toolsets.IsEmpty
            ? new LegacyToolRunCatalogCaptureFactory(_legacyCatalog).Create(request)
            : CreateAsync(request, CancellationToken.None).AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async ValueTask<IToolCatalogCapture> CreateAsync(
        RunToolCatalogCaptureRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Toolsets.IsEmpty)
        {
            return new LegacyToolRunCatalogCaptureFactory(_legacyCatalog).Create(request);
        }

        ArgumentNullException.ThrowIfNull(request.Configuration);
        ArgumentNullException.ThrowIfNull(request.ModelCapabilities);
        var discovery = new ToolDiscoveryRequest(
            request.AgentId,
            request.SessionId,
            request.RunId,
            request.Authorization.Identity,
            request.Authorization,
            request.Authorization.AgentDefinitionRevision,
            request.Configuration,
            request.Toolsets,
            request.ModelCapabilities);

        var result = await _coordinator.CaptureAsync(discovery, cancellationToken).ConfigureAwait(false);
        return result switch
        {
            ToolCatalogCoordinatorCaptured captured => captured.Catalog,
            ToolCatalogCoordinatorMergeRejected rejected => throw new InvalidOperationException(
                $"Tool catalog merge rejected: {rejected.Context.SafeReason}"),
            ToolCatalogCoordinatorSchemaRejected schemaRejected => throw new InvalidOperationException(
                $"Tool schema preflight rejected '{schemaRejected.Tool.Id}': {schemaRejected.Rejection.SafeReason}"),
            _ => throw new InvalidOperationException("Tool catalog capture returned an unsupported result."),
        };
    }
}
