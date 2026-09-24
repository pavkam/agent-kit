// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Builds run-bound <see cref="IToolCatalogCapture"/> evidence through the configured <see cref="IToolCatalog"/>.</summary>
public sealed class ToolRunCatalogCaptureFactory: IToolRunCatalogCaptureFactory
{
    private readonly IToolCatalog _catalog;

    /// <summary>Initializes the capture factory.</summary>
    /// <param name="catalog">The nonnull catalog coordinator used for every capture.</param>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is null.</exception>
    public ToolRunCatalogCaptureFactory(IToolCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    /// <inheritdoc/>
    public IToolCatalogCapture Create(RunToolCatalogCaptureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CreateAsync(request).AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async ValueTask<IToolCatalogCapture> CreateAsync(
        RunToolCatalogCaptureRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Toolsets.IsEmpty)
        {
            ArgumentNullException.ThrowIfNull(request.Configuration);
            ArgumentNullException.ThrowIfNull(request.ModelCapabilities);
        }

        var configuration = request.Configuration
            ?? new EffectiveConfigurationSnapshot(
                request.Authorization.ConfigurationVersion,
                new ContentHash($"sha256:tool-catalog:{request.Authorization.ConfigurationVersion.Value}"),
                [],
                []);
        var modelCapabilities = request.ModelCapabilities
            ?? new ModelCapabilities(true, true, true, true, false, false, false, ExtensionData.Empty);

        var discovery = new ToolDiscoveryRequest(
            request.AgentId,
            request.SessionId,
            request.RunId,
            request.Authorization.Identity,
            request.Authorization,
            request.Authorization.AgentDefinitionRevision,
            configuration,
            request.Toolsets,
            modelCapabilities);

        return await _catalog.CaptureAsync(discovery, cancellationToken).ConfigureAwait(false);
    }
}
