// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Exposes a typed controllable catalog policy seam without invoking reflection or external services.</summary>
public sealed class CallbackToolCatalogMergePolicy: IToolCatalogMergePolicy
{
    /// <summary>Gets or sets the callback to invoke after argument validation.</summary>
    /// <value>Null returns rejection; a callback may arrange asynchronous completion, cancellation, or a deliberately invalid policy result.</value>
    public Func<ToolCatalogMergeContext, CancellationToken, ValueTask<ToolCatalogMergeDecision>>? Resolve { get; set; }

    /// <inheritdoc/>
    public ValueTask<ToolCatalogMergeDecision> ResolveAsync(ToolCatalogMergeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Resolve?.Invoke(context, cancellationToken) ?? ValueTask.FromResult<ToolCatalogMergeDecision>(new ToolCatalogRejection());
    }
}
