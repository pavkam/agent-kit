// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

/// <summary>Records formatted store log events so tests can verify identifiers and absence of protected request content.</summary>
internal sealed class RecordingSessionStoreLogger: ILogger<InMemorySessionStore>
{
    private readonly ConcurrentQueue<(EventId EventId, string Message)> _events = [];

    /// <summary>Gets a stable snapshot of recorded events.</summary>
    /// <returns>The ordered event identifiers and safely formatted messages observed so far.</returns>
    internal ImmutableArray<(EventId EventId, string Message)> Snapshot() => [.. _events];

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        _events.Enqueue((eventId, formatter(state, exception)));
    }
}
