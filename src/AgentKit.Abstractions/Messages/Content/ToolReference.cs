// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies the tool referenced by a <see cref="ToolCallPart"/> or
/// <see cref="ToolResultPart"/>, including exactly what was advertised to
/// the model at call time and, once resolution succeeds, the canonical
/// identity it resolved to.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// <see cref="ProviderAlias"/> is deliberately captured separately from
/// <see cref="Id"/>: the alias is whatever text was actually advertised to,
/// or received back from, the model — including a hallucinated name the
/// model invented for a tool that was never registered — while
/// <see cref="Id"/> is the stable AgentKit identity resolved for it, once a
/// later resolution step matches the alias against the registered tool
/// catalog. A parser that only observes the model's raw response can never
/// prove resolution succeeded, so it always leaves <see cref="Id"/> and
/// <see cref="Version"/> null; only the component that actually consults the
/// tool catalog may populate them.
/// </para>
/// <para>
/// <see cref="Id"/> and <see cref="Version"/> are both present or both
/// absent: a resolved reference always carries the exact version the
/// catalog snapshot bound to that identity, and an unresolved reference
/// never claims a canonical identity it did not earn.
/// </para>
/// </remarks>
public sealed record ToolReference
{
    /// <summary>Initializes a new instance of the <see cref="ToolReference"/> record.</summary>
    /// <param name="providerAlias">The exact provider-visible alias advertised to, or received from, the model.</param>
    /// <param name="id">The resolved stable tool identity, or null when the reference is not yet resolved.</param>
    /// <param name="version">
    /// The resolved tool version, present exactly when <paramref name="id"/> is present.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="providerAlias"/> is default, <paramref name="id"/> is present but default,
    /// <paramref name="version"/> is present but default, or exactly one of <paramref name="id"/> and
    /// <paramref name="version"/> is present without the other.
    /// </exception>
    public ToolReference(ToolAlias providerAlias, ToolId? id, ToolVersion? version)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(providerAlias, default);
        ArgumentException.ThrowIfNotEqual(id.HasValue, version.HasValue, nameof(version));
        if (id is { } resolvedId)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(resolvedId, default, nameof(id));
        }

        if (version is { } resolvedVersion)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(resolvedVersion, default, nameof(version));
        }

        ProviderAlias = providerAlias;
        Id = id;
        Version = version;
    }

    /// <summary>Gets the exact provider-visible alias advertised to, or received from, the model.</summary>
    /// <value>A nondefault alias; always present regardless of whether resolution succeeded.</value>
    public ToolAlias ProviderAlias
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default);
            field = value;
        }
    }

    /// <summary>Gets the resolved stable tool identity.</summary>
    /// <value>Null exactly when this reference has not been resolved against a tool catalog.</value>
    public ToolId? Id { get; }

    /// <summary>Gets the resolved tool version.</summary>
    /// <value>Present exactly when <see cref="Id"/> is present.</value>
    public ToolVersion? Version { get; }

    /// <summary>Gets whether this reference resolved to a canonical tool identity.</summary>
    /// <value><see langword="true"/> exactly when <see cref="Id"/> is present.</value>
    public bool IsResolved => Id.HasValue;
}
