// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.InMemory.Tests;

using Microsoft.Extensions.Logging;

/// <summary>Records formatted grant-store events so tests can verify stable identifiers and content safety.</summary>
internal sealed class RecordingSecurityGrantStoreLogger: ILogger<InMemorySecurityGrantStore>
{
    /// <summary>Gets the events emitted by one isolated test store.</summary>
    /// <value>The ordered structured event identifiers and formatted safe messages.</value>
    internal List<(EventId EventId, string Message)> Events { get; } = [];

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        Events.Add((eventId, formatter(state, exception)));
    }
}
