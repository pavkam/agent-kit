// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Selects one registered durable operation journal, which owns checkpoint
/// and terminal-result truth for recoverable operations.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal, textual)
/// equality over <see cref="Value"/>. It carries no mutable state and is safe
/// to share and compare across threads without synchronization.
/// </para>
/// <para>
/// The journal is keyed separately from the backend because they are
/// independent choices. A workflow engine may own dispatch while a local
/// database owns the journal, and recovery must rebind the exact journal that
/// holds an operation's evidence. Reading evidence from a different journal
/// would report work as never started when it merely lives elsewhere.
/// </para>
/// </remarks>
public readonly record struct DurableJournalKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DurableJournalKey"/>
    /// struct, validating that it carries usable key text.
    /// </summary>
    /// <param name="value">The non-empty canonical journal key.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public DurableJournalKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical journal key text.</summary>
    public string Value { get; }

    /// <summary>
    /// Returns the canonical journal key text, suitable for logging and
    /// composition-validation messages.
    /// </summary>
    public override string ToString() => Value;
}
