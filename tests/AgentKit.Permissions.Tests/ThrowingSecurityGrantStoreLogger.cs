// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

using Microsoft.Extensions.Logging;

/// <summary>Throws from every enabled log write to prove grant consumption is isolated from hostile observers.</summary>
internal sealed class ThrowingSecurityGrantStoreLogger: ILogger<InMemorySecurityGrantStore>
{
    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        throw new InvalidOperationException("logger failure");
}
