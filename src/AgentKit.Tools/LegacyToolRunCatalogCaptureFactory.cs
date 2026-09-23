// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Builds run-bound catalog captures from the legacy singleton catalog or through discovery when toolsets are authored.</summary>
public sealed class LegacyToolRunCatalogCaptureFactory: IToolRunCatalogCaptureFactory
{
    private readonly IToolCatalog _legacyCatalog;
    private readonly IServiceProvider _services;

    /// <summary>Initializes the capture factory.</summary>
    /// <param name="legacyCatalog">The legacy singleton catalog.</param>
    /// <param name="services">The composition root used to resolve the internal discovery coordinator when present.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public LegacyToolRunCatalogCaptureFactory(IToolCatalog legacyCatalog, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(legacyCatalog);
        ArgumentNullException.ThrowIfNull(services);
        _legacyCatalog = legacyCatalog;
        _services = services;
    }

    /// <inheritdoc/>
    public IToolCatalogCapture Create(RunToolCatalogCaptureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Toolsets.IsEmpty
            ? CreateLegacy(request)
            : throw new InvalidOperationException("Toolset-driven catalog capture requires CreateAsync.");
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
            return CreateLegacy(request);
        }

        var coordinator = _services.GetService<ToolCatalogCoordinator>()
            ?? throw new InvalidOperationException("Toolset-driven capture requires AddToolCatalogCoordinator.");
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

        var result = await coordinator.CaptureAsync(discovery, cancellationToken).ConfigureAwait(false);
        return result switch
        {
            ToolCatalogCoordinatorCaptured captured => captured.Catalog,
            ToolCatalogCoordinatorMergeRejected => throw new InvalidOperationException("Tool catalog merge rejected the discovered contributions."),
            ToolCatalogCoordinatorSchemaRejected schemaRejected => throw new InvalidOperationException(
                $"Tool schema preflight rejected '{schemaRejected.Tool.Id}'."),
            _ => throw new InvalidOperationException("Tool catalog capture returned an unsupported result."),
        };
    }

    private LegacyToolCatalogCapture CreateLegacy(RunToolCatalogCaptureRequest request)
    {
        var snapshot = LegacyToolCatalogSnapshotFactory.Create(
            _legacyCatalog,
            request.AgentId,
            request.SessionId,
            request.RunId,
            request.Authorization);
        return new LegacyToolCatalogCapture(snapshot);
    }
}
