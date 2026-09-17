// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using System.Collections.Immutable;

using Microsoft.Extensions.Logging;

/// <summary>Captures structured logs and supports deliberate observer failure in diagnostic contract tests.</summary>
/// <typeparam name="T">The emitting type whose logger category is preserved.</typeparam>
public sealed class RecordingLogger<T>: ILogger<T>
{
    private readonly ConcurrentQueue<RecordingLogEntry> _entries = new();

    /// <summary>Gets or sets whether writes throw before recording, to exercise observer isolation.</summary>
    public bool ThrowOnWrite { get; set; }

    /// <summary>Returns a stable snapshot of captured entries in callback order.</summary>
    /// <returns>The recorded log entries.</returns>
    public RecordingLogEntry[] Snapshot() => [.. _entries];

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (ThrowOnWrite)
        {
            throw new InvalidOperationException("test logger failure");
        }

        var fields = state is IEnumerable<KeyValuePair<string, object?>> values
            ? values.ToImmutableDictionary(StringComparer.Ordinal)
            : [];
        if (state is IReadOnlyList<KeyValuePair<string, object?>> indexable)
        {
            // Exercises the source-generated structured state's indexer and non-generic enumerator,
            // matching how a real structured-logging exporter (for example OpenTelemetry) walks tags.
            for (var index = 0; index < indexable.Count; index++)
            {
                _ = indexable[index];
            }

            foreach (var _ in (System.Collections.IEnumerable) indexable)
            {
            }
        }

        _entries.Enqueue(new(typeof(T).FullName!, logLevel, eventId, fields, formatter(state, exception)));
    }
}
