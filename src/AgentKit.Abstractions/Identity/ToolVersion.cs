// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one published version of a <see cref="ToolId"/>, such as a
/// schema or behavior revision of a tool that has been updated since an
/// earlier call was recorded.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>. It carries no mutable state
/// itself and is safe to share and compare across threads without
/// synchronization.
/// </para>
/// <para>
/// A run's tool catalog snapshot binds a provider alias to one exact
/// <see cref="ToolId"/>/<see cref="ToolVersion"/> pair for the
/// lifetime of a run, so a model's tool call always resolves against the
/// version that was actually advertised in that run's request, not
/// whichever version happens to be registered when the call is later
/// invoked.
/// </para>
/// </remarks>
public readonly record struct ToolVersion
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ToolVersion"/> struct,
    /// validating that it carries usable version text.
    /// </summary>
    /// <param name="value">The non-empty canonical tool version text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public ToolVersion(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical tool version text.</summary>
    public string Value { get; }

    /// <summary>Returns the canonical version text.</summary>
    public override string ToString() => Value;
}
