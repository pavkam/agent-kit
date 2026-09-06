// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one additive agent-definition source contributing definitions
/// to the engine-wide catalog — for example, one source might load
/// statically configured definitions while another polls a remote
/// configuration service.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>. It carries no mutable state
/// itself and is safe to share and compare across threads without
/// synchronization.
/// </para>
/// <para>
/// The engine-wide definition catalog composes many sources, each
/// identified by an <see cref="AgentDefinitionSourceId"/>, into one
/// immutable, versioned snapshot. Each source also has an explicit
/// precedence, so when two sources publish conflicting revisions for the
/// same agent identity, the catalog can resolve the conflict deterministically
/// instead of racing on load order.
/// </para>
/// </remarks>
public readonly record struct AgentDefinitionSourceId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentDefinitionSourceId"/>
    /// struct, validating that it carries usable identifier text.
    /// </summary>
    /// <param name="value">The non-empty canonical source identifier text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public AgentDefinitionSourceId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical source identifier text.</summary>
    public string Value { get; }

    /// <summary>Returns the canonical identifier text.</summary>
    public override string ToString() => Value;
}
