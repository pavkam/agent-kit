// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json;

/// <summary>Portable JSON mirror of <see cref="ProtectedResource"/>, naming one canonical resource to which authority is narrowly bound.</summary>
/// <remarks>
/// <para>
/// A resource is the narrowest thing a grant binds to, so both its class and its canonical identifier are persisted verbatim.
/// <see cref="ProtectedResourceKind"/> is a domain enum and is stored directly; the identifier stays the exact canonical text
/// the effecting boundary produced, because an effecting boundary re-derives and compares that text before acting and any
/// normalization applied here would silently authorize a different concrete effect.
/// </para>
/// <para>
/// This type is an immutable value with structural equality over its primitive members, so ordered resource sequences compare
/// element-wise as the domain requires.
/// </para>
/// </remarks>
/// <param name="Kind">The resource class.</param>
/// <param name="Identifier">The non-blank canonical identifier produced by the effecting boundary.</param>
public sealed record JsonProtectedResource(
    ProtectedResourceKind Kind,
    string Identifier)
{
    /// <summary>Projects one domain protected resource into its portable JSON representation.</summary>
    /// <param name="value">The non-null resource to project.</param>
    /// <returns>A document carrying the resource class and its exact canonical identifier.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static JsonProtectedResource FromDomain(ProtectedResource value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new JsonProtectedResource(value.Kind, value.Identifier);
    }

    /// <summary>Reconstructs the exact domain resource this document was projected from.</summary>
    /// <returns>A resource equal to the projected original.</returns>
    /// <remarks>
    /// Reconstruction goes through <see cref="ProtectedResource(ProtectedResourceKind, string)"/>, so an undefined persisted
    /// kind or a blank identifier fails closed rather than producing a resource that a later grant comparison would treat as
    /// a match.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="Kind"/> is not a defined <see cref="ProtectedResourceKind"/> value.</exception>
    /// <exception cref="ArgumentException"><see cref="Identifier"/> is null, empty, or whitespace.</exception>
    public ProtectedResource ToDomain() => new ProtectedResource(Kind, Identifier);
}
