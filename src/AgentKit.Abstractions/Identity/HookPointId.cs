// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one named lifecycle boundary — a hook point — such as
/// "session.creating" or "compaction.completed", independent of the
/// specific hook interface and event-argument types that implement it.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>, safe to share and compare
/// across threads without synchronization.
/// </para>
/// <para>
/// A <see cref="HookId"/> identifies one hook registration; a
/// <see cref="HookPointId"/> identifies the boundary that registration was
/// made for. The dispatcher uses this value to scope ordering constraints,
/// duplicate detection, and reentrancy tracking to hooks that were actually
/// registered for the same boundary, and never compares ordering or
/// reentrancy state across two different points.
/// </para>
/// </remarks>
public readonly record struct HookPointId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HookPointId"/> struct,
    /// validating that it carries usable identifier text.
    /// </summary>
    /// <param name="value">The non-empty canonical hook point identifier text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public HookPointId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical hook point identifier text.</summary>
    public string Value { get; }

    /// <summary>Returns the canonical identifier text.</summary>
    public override string ToString() => Value;
}
