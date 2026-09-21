// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Resolves the tool definitions advertised for one model request.</summary>
public interface IToolSnapshotProvider
{
    /// <summary>Resolves tool definitions for one assembly evaluation.</summary>
    /// <param name="request">The immutable snapshot evidence.</param>
    /// <param name="cancellationToken">Cancels resolution before it returns.</param>
    /// <returns>The bounded tool definitions to advertise to the model.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    public ValueTask<ImmutableArray<LlmToolDefinition>> ResolveToolsAsync(
        ToolSnapshotRequest request,
        CancellationToken cancellationToken = default);
}
