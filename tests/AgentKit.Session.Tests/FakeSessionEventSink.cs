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

    public ValueTask PublishAsync(SessionEvent sessionEvent, CancellationToken cancellationToken = default)
    {
        Received.Add(sessionEvent);
        return ValueTask.CompletedTask;
    }
}
