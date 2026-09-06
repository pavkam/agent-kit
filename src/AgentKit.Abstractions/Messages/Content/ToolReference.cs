// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies the tool referenced by a <see cref="ToolCallPart"/> or
/// <see cref="ToolResultPart"/>, including exactly what was advertised to
/// the model at call time.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// <see cref="Name"/> is deliberately captured separately from
/// <see cref="Id"/>: the name is whatever text was actually advertised to
/// the model in the request that produced this call, while <see cref="Id"/>
/// is the stable AgentKit identity resolved for it. If the tool catalog
/// changes between when a call is requested and when it is later inspected,
/// the recorded <see cref="Name"/> still reflects history accurately
/// instead of silently reporting whatever name the tool happens to have
/// now.
/// </para>
/// </remarks>
public sealed record ToolReference
{
    /// <summary>Initializes a new instance of the <see cref="ToolReference"/> record.</summary>
    /// <param name="id">The stable tool identity.</param>
    /// <param name="version">
    /// The resolved tool version, when the run's tool catalog snapshot
    /// resolved one; <see langword="null"/> when the tool is unversioned.
    /// </param>
    /// <param name="name">
    /// The tool name exactly as advertised to the model at the time of the
    /// call.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public ToolReference(ToolId id, ToolVersion? version, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Id = id;
        Version = version;
        Name = name;
    }

    /// <summary>Gets the stable tool identity.</summary>
    public ToolId Id { get; init; }

    /// <summary>
    /// Gets the resolved tool version, when the run's tool catalog snapshot
    /// resolved one.
    /// </summary>
    public ToolVersion? Version { get; init; }

    /// <summary>
    /// Gets the tool name exactly as advertised to the model at the time of
    /// the call.
    /// </summary>
    public string Name { get; init; }
}
