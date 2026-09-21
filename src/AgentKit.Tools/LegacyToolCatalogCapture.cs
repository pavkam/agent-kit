// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>
/// A reduced run-bound catalog capture that exposes only immutable snapshot evidence for the legacy
/// <see cref="DefaultToolInvoker"/> adapter; it does not acquire invoker leases.
/// </summary>
/// <remarks>
/// This type exists only until workstream 4 retires the legacy orchestrator (chunk C10a). The executor adapter
/// resolves tools through <see cref="IToolInvoker"/> instead of <see cref="IToolCatalogCapture.AcquireInvokerAsync"/>.
/// </remarks>
public sealed class LegacyToolCatalogCapture(ToolCatalogSnapshot snapshot): IToolCatalogCapture
{
    /// <inheritdoc/>
    public ToolCatalogSnapshot Snapshot { get; } = snapshot ?? throw new ArgumentNullException(nameof(snapshot));

    /// <inheritdoc/>
    public ValueTask<ToolInvokerLeaseResult> AcquireInvokerAsync(ToolIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(identity, default);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<ToolInvokerLeaseResult>(
            new ToolInvokerUnavailable(identity, "The legacy catalog capture does not acquire invoker leases."));
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
