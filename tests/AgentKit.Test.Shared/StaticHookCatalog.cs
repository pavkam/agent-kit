// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Returns one fixed <see cref="HookCatalogSnapshot"/> from every capture request.</summary>
public sealed class StaticHookCatalog: IHookCatalog
{
    private readonly HookCatalogSnapshot _snapshot;

    /// <summary>Initializes a new instance of the <see cref="StaticHookCatalog"/> class.</summary>
    /// <param name="snapshot">The snapshot every capture returns.</param>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
    public StaticHookCatalog(HookCatalogSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _snapshot = snapshot;
    }

    /// <inheritdoc/>
    public ValueTask<HookCatalogSnapshot> CaptureAsync(HookCatalogRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<HookCatalogSnapshot>(_snapshot);
    }
}
