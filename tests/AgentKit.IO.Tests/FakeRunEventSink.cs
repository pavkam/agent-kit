// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

/// <summary>An <see cref="IRunEventSink"/> test double whose delivery is scripted per test.</summary>
internal sealed class FakeRunEventSink: IRunEventSink
{
    private readonly Func<RunEvent, CancellationToken, ValueTask>? _publish;

    /// <summary>Initializes a sink that completes immediately unless <paramref name="publish"/> is supplied.</summary>
    /// <param name="publish">An optional delegate that replaces the default immediate-completion behavior.</param>
    public FakeRunEventSink(Func<RunEvent, CancellationToken, ValueTask>? publish = null) => _publish = publish;

    /// <summary>Gets every event this fake received, in call order.</summary>
    public List<RunEvent> Received { get; } = [];

    /// <inheritdoc/>
    public ValueTask PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default)
    {
        Received.Add(runEvent);
        return _publish is null ? ValueTask.CompletedTask : _publish(runEvent, cancellationToken);
    }
}
