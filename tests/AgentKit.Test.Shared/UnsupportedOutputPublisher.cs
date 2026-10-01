// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>An <see cref="IOutputPublisher"/> test double that throws <see cref="NotSupportedException"/> from every member.</summary>
/// <remarks>A placeholder collaborator for tests whose scripted loop never publishes run events.</remarks>
public sealed class UnsupportedOutputPublisher: IOutputPublisher
{
    /// <inheritdoc/>
    public ValueTask PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This test double does not support event publication.");

    /// <inheritdoc/>
    public ValueTask CompleteAsync<TOutput>(AgentRunFinished<TOutput> result, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This test double does not support result publication.");
}
