// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>
/// Declares which optional behaviors a conformance subject implements.
/// </summary>
/// <remarks>
/// <para>
/// Suites may skip a case only when that case is documented as optional and
/// the fixture set the matching flag to <see langword="false"/> before the
/// case runs. Required contract behavior always runs. Flags never authorize
/// inspecting private state, weakening a denial, or treating a missing
/// guarantee as success.
/// </para>
/// <para>
/// <see cref="All"/> and <c>new ConformanceCapabilities()</c> require every
/// optional behavior. This is a class, not a struct, so an uninitialized
/// field cannot silently report both flags false.
/// </para>
/// </remarks>
public sealed record ConformanceCapabilities
{
    /// <summary>Gets the declaration that every optional behavior is supported.</summary>
    public static ConformanceCapabilities All { get; } = new();

    /// <summary>Initializes a capability declaration.</summary>
    /// <param name="supportsDurability">
    /// Whether acknowledged state survives disposal of the subject and is visible
    /// to a later <c>CreateAsync</c> from the same fixture. In-memory subjects pass <see langword="false"/>.
    /// </param>
    /// <param name="supportsConcurrentCreators">
    /// Whether overlapping <c>CreateAsync</c> calls are supported. A subject that
    /// rejects a second creator passes <see langword="false"/>; sequential cases still run.
    /// </param>
    public ConformanceCapabilities(bool supportsDurability = true, bool supportsConcurrentCreators = true)
    {
        SupportsDurability = supportsDurability;
        SupportsConcurrentCreators = supportsConcurrentCreators;
    }

    /// <summary>Gets whether acknowledged state survives disposal.</summary>
    public bool SupportsDurability { get; }

    /// <summary>Gets whether overlapping <c>CreateAsync</c> calls are supported.</summary>
    public bool SupportsConcurrentCreators { get; }
}
