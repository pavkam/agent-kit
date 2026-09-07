// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using Microsoft.Extensions.Logging;

/// <summary>A hostile logger proving diagnostic failures cannot change admission semantics.</summary>
internal sealed class ThrowingAgentEngineLogger: ILogger<AgentEngine>
{
    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        throw new InvalidOperationException("Hostile logger.");
}
