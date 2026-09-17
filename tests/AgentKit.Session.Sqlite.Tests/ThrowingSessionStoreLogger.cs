// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite.Tests;

using Microsoft.Extensions.Logging;

/// <summary>Throws from every enabled log write to verify that ordinary diagnostics cannot control session effects.</summary>
internal sealed class ThrowingSessionStoreLogger: ILogger<SqliteSessionStore>
{
    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) => throw new InvalidOperationException("logger failure");
}
