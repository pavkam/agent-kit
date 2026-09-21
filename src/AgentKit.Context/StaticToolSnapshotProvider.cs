// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context;

/// <summary>Projects a captured <see cref="ToolCatalogSnapshot"/> or falls back to caller-supplied tool definitions.</summary>
internal sealed class StaticToolSnapshotProvider: IToolSnapshotProvider
{
    /// <inheritdoc/>
    public ValueTask<ImmutableArray<LlmToolDefinition>> ResolveToolsAsync(
        ToolSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Catalog is not { } catalog)
        {
            return ValueTask.FromResult(request.FallbackTools);
        }

        var builder = ImmutableArray.CreateBuilder<LlmToolDefinition>(catalog.Tools.Length);
        foreach (var descriptor in catalog.Tools)
        {
            builder.Add(new LlmToolDefinition(
                descriptor.Id,
                descriptor.Name,
                descriptor.Description,
                descriptor.InputSchema.Document));
        }

        return ValueTask.FromResult(builder.ToImmutable());
    }
}
