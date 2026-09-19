// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

/// <summary>
/// An <see cref="IOutputPublisher"/> test double that records every published event in order and, when scripted
/// to fail, throws a fixed exception from <see cref="PublishAsync"/> instead of recording.
/// </summary>
/// <remarks>
/// <see cref="CompleteAsync{TOutput}"/> is unsupported: no loop behavior exercised through this double completes
/// a run through the publisher, it only publishes in-run events.
/// </remarks>
internal sealed class RecordingOutputPublisher: IOutputPublisher
{
    private readonly List<RunEvent> _events = [];
    private readonly Exception? _failure;

    /// <summary>Initializes a publisher that records every event, optionally failing every call instead.</summary>
    /// <param name="failure">
    /// When supplied, every call to <see cref="PublishAsync"/> throws this exception instead of recording the
    /// event; when <see langword="null"/>, every call records and succeeds.
    /// </param>
    public RecordingOutputPublisher(Exception? failure = null) => _failure = failure;

    /// <summary>Gets every event published so far, in publication order.</summary>
    /// <value>A live, read-only view over the recorded events.</value>
    public IReadOnlyList<RunEvent> Events => _events;

    /// <inheritdoc/>
    public ValueTask PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default)
    {
        if (_failure is { } failure)
        {
            throw failure;
        }

        _events.Add(runEvent);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask CompleteAsync<TOutput>(AgentRunFinished<TOutput> result, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("No behavior exercised through this double completes a run through the publisher.");
}
