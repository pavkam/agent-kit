// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Supplies deterministic or deliberately failing timestamp measurements at a fixed test frequency.</summary>
/// <remarks>It is intended for duration instrumentation; callbacks select every returned timestamp or failure.</remarks>
public sealed class CallbackTimestampTimeProvider: TimeProvider
{
    private readonly Func<long> _timestamp;

    /// <summary>Captures a nonnull callback without reading the clock.</summary>
    /// <param name="timestamp">The callback invoked for each timestamp measurement.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timestamp"/> is null.</exception>
    public CallbackTimestampTimeProvider(Func<long> timestamp)
    {
        ArgumentNullException.ThrowIfNull(timestamp);
        _timestamp = timestamp;
    }

    /// <summary>Gets the fixed conversion frequency used by duration assertions.</summary>
    /// <value>One thousand ticks per second.</value>
    public override long TimestampFrequency => 1000;

    /// <summary>Obtains the next controlled timestamp without using a system clock.</summary>
    /// <returns>The supplied callback's value; callback exceptions propagate unchanged.</returns>
    public override long GetTimestamp() => _timestamp();
}
