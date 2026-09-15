// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

/// <summary>Projects model-attempt events into one run's best-effort observer.</summary>
internal sealed class RunModelResponseObserver: IModelResponseObserver
{
    private readonly Func<AgentRunEvent, CancellationToken, ValueTask> _observe;
    private readonly TurnId _turnId;

    /// <summary>Initializes an observer for one turn.</summary>
    /// <param name="turnId">The turn containing the model attempt.</param>
    /// <param name="observe">The isolated run-observer delivery callback.</param>
    /// <exception cref="ArgumentNullException"><paramref name="observe"/> is null.</exception>
    internal RunModelResponseObserver(
        TurnId turnId,
        Func<AgentRunEvent, CancellationToken, ValueTask> observe)
    {
        ArgumentNullException.ThrowIfNull(observe);
        _turnId = turnId;
        _observe = observe;
    }

    /// <inheritdoc/>
    public ValueTask OnEventAsync(
        ModelResponseEvent responseEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(responseEvent);
        return _observe(new AgentRunModelResponseEvent(_turnId, responseEvent), cancellationToken);
    }
}
