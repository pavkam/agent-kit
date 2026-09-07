// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The stable, author-supplied identity of one hook registration, used for
/// ordering constraints (<see cref="IHook.RunsBefore"/>,
/// <see cref="IHook.RunsAfter"/>, <see cref="IHook.DependsOn"/>),
/// diagnostics, and duplicate-registration detection.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>, safe to share and compare
/// across threads without synchronization.
/// </para>
/// <para>
/// Unlike <see cref="HookInvocationId"/>, which is minted fresh for every
/// dispatch, a <see cref="HookId"/> is chosen once by whoever authors a hook
/// implementation and stays stable across the implementation's lifetime; it
/// is how one hook refers to another in an ordering constraint, and how the
/// dispatcher detects that two registrations for the same hook point claim
/// the same identity.
/// </para>
/// </remarks>
public readonly record struct HookId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HookId"/> struct,
    /// validating that it carries usable identifier text.
    /// </summary>
    /// <param name="value">The non-empty canonical hook identifier text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public HookId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical hook identifier text.</summary>
    public string Value { get; }

    /// <summary>Returns the canonical identifier text.</summary>
    public override string ToString() => Value;
}
