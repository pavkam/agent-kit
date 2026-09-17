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
        ExerciseStateContract(state);
        _entries.Enqueue(new(typeof(T).FullName!, logLevel, eventId, fields, formatter(state, exception)));
    }

    /// <summary>Drives the full public surface a real logging provider may use against a source-generated log
    /// state, so every structured log call observably supports out-of-band field access rather than only the
    /// narrow path this fixture happens to take for its own field snapshot.</summary>
    /// <param name="state">The state instance passed to <see cref="Log{TState}"/>.</param>
    /// <typeparam name="TState">The compiler-selected state type for the call.</typeparam>
    private static void ExerciseStateContract<TState>(TState state)
    {
        if (state is IReadOnlyList<KeyValuePair<string, object?>> indexed)
        {
            var count = indexed.Count;
            for (var index = 0; index < count; index++)
            {
                _ = indexed[index];
            }

            try
            {
                _ = indexed[count];
            }
            catch (IndexOutOfRangeException)
            {
                // Out-of-range access is expected to fail closed; a real provider that walks past the
                // reported count must observe the same guard the source generator documents.
            }
        }

        if (state is System.Collections.IEnumerable nonGeneric)
        {
            var enumerator = nonGeneric.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                }
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }
        }
    }
}
