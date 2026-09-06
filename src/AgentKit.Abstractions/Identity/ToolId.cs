// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one tool, independent of the provider-facing alias used to
/// advertise it in a model request and independent of the specific
/// <see cref="ToolVersion"/> currently published for it.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>. It carries no mutable state
/// itself and is safe to share and compare across threads without
/// synchronization.
/// </para>
/// <para>
/// A <see cref="ToolReference"/> pairs a <see cref="ToolId"/> with an
/// optional <see cref="ToolVersion"/> and the display name that was
/// actually advertised to the model at call time, because catalog
/// evolution can publish a new version of a tool between when a call is
/// requested and when it is recorded. Keeping identity, version, and
/// advertised name distinct lets the runtime detect and handle that kind of
/// drift explicitly instead of silently resolving a call against whichever
/// version happens to be current when it executes.
/// </para>
/// </remarks>
public readonly record struct ToolId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ToolId"/> struct,
    /// validating that it carries usable identifier text.
    /// </summary>
    /// <param name="value">The non-empty canonical tool identifier text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public ToolId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical tool identifier text.</summary>
    public string Value { get; }

    /// <summary>Returns the canonical identifier text.</summary>
    public override string ToString() => Value;
}
