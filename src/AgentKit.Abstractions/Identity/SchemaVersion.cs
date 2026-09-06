// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies the durable schema version of a serialized record, independent
/// of the assembly version of the code that wrote it. Readers use this
/// value to reject unsupported major versions and to select the correct
/// migration path for older data.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>. It carries no mutable state
/// itself and is safe to share and compare across threads without
/// synchronization.
/// </para>
/// <para>
/// Assembly version and schema version answer different questions: an
/// assembly can be upgraded many times without ever changing how a record
/// is serialized, and a record's durable schema can be versioned
/// independently of whatever code currently happens to read or write it.
/// Keeping <see cref="SchemaVersion"/> distinct from the CLR type's own
/// version lets AgentKit evolve durable formats deliberately, with explicit
/// migrations, instead of accidentally breaking storage compatibility every
/// time a package is rebuilt.
/// </para>
/// </remarks>
public readonly record struct SchemaVersion
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaVersion"/>
    /// struct, validating that it carries usable version text.
    /// </summary>
    /// <param name="value">The non-empty canonical schema version text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public SchemaVersion(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical schema version text.</summary>
    public string Value { get; }

    /// <summary>Returns the canonical schema version text.</summary>
    public override string ToString() => Value;
}
