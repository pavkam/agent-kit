// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Builds <see cref="LegacyToolCatalogCapture"/> instances from the registered legacy singleton catalog.</summary>
public sealed class LegacyToolRunCatalogCaptureFactory(IToolCatalog catalog): IToolRunCatalogCaptureFactory
{
    /// <inheritdoc/>
    public IToolCatalogCapture Create(RunToolCatalogCaptureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var snapshot = LegacyToolCatalogSnapshotFactory.Create(
            catalog,
            request.AgentId,
            request.SessionId,
            request.RunId,
            request.Authorization);
        return new LegacyToolCatalogCapture(snapshot);
    }
}
