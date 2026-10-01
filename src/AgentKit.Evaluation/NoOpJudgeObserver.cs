// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Discards model response events because a judge needs only the final reply.</summary>
internal sealed class NoOpJudgeObserver: IModelResponseObserver
{
    /// <summary>Gets the shared stateless observer.</summary>
    internal static NoOpJudgeObserver Instance { get; } = new();

    /// <inheritdoc/>
    public ValueTask OnEventAsync(ModelResponseEvent responseEvent, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
