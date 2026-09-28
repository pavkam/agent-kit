// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Selects one keyed observation exporter registration in the host composition.</summary>
/// <remarks>This immutable value uses ordinal text equality and carries selection identity only; it does not export signals by itself.</remarks>
public readonly record struct ObservationExporterKey
{
    /// <summary>Initializes a validated exporter selection key.</summary>
    /// <param name="value">The non-blank canonical exporter key.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or consists only of whitespace.</exception>
    public ObservationExporterKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical exporter key text.</summary>
    /// <value>The exact caller-supplied text. A default instance exposes <see langword="null"/> at runtime.</value>
    public string Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value ?? string.Empty;
}
