// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>
/// An <see cref="IModelRequestExecutor"/> test double that throws when executed.
/// </summary>
public sealed class UnsupportedModelRequestExecutor: IModelRequestExecutor
{
    /// <inheritdoc/>
    public Task<ModelExecutionResult> ExecuteAsync(
        ModelExecutionRequest request,
        IModelResponseObserver observer,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This test double does not support model execution.");
}
