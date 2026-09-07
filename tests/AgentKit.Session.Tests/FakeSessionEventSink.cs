// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

/// <summary>
/// An <see cref="ISessionEventSink"/> test double that records every
/// published event in delivery order.
/// </summary>
internal sealed class FakeSessionEventSink: ISessionEventSink
{
    public List<SessionEvent> Received { get; } = [];

    public Func<SessionEvent, CancellationToken, ValueTask>? OnPublish { get; set; }

    public ValueTask PublishAsync(SessionEvent sessionEvent, CancellationToken cancellationToken = default)
    {
        Received.Add(sessionEvent);
        return OnPublish?.Invoke(sessionEvent, cancellationToken) ?? ValueTask.CompletedTask;
    }
}
