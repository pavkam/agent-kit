// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

/// <summary>
/// An <see cref="IModelResponseObserver"/> that discards every delivered
/// event.
/// </summary>
/// <remarks>
/// <see cref="DefaultAgentLoop"/> uses this observer because there is no
/// not-yet-implemented AgentKit.IO/AgentKit.Output live event fan-out to
/// forward streamed events to. It exists so <see cref="IChatModel.ExecuteAsync"/>
/// always has a valid observer to deliver its ordered event sequence to,
/// even though this reduced loop only consumes the attempt's terminal
/// <see cref="ModelAttemptResult"/>.
/// </remarks>
internal sealed class NoOpModelResponseObserver: IModelResponseObserver
{
    /// <summary>Gets the shared, stateless instance of this observer.</summary>
    public static NoOpModelResponseObserver Instance { get; } = new();

    private NoOpModelResponseObserver()
    {
    }

    /// <inheritdoc/>
    public ValueTask OnEventAsync(ModelResponseEvent responseEvent, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}
