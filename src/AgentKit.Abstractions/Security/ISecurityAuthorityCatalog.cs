// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Answers, from already-registered immutable bindings, whether a security authority key is installed, so composition
/// validation can prove a definition's exact security publication names a real authority before any run starts.
/// </summary>
/// <remarks>
/// <para>
/// This is a validation-time companion to <see cref="ISecurityAuthoritySelector"/>, which resolves an authority for one
/// captured authorization. It performs no I/O, never invokes an authority, and grants nothing: presence of a binding
/// is composition evidence, not permission.
/// </para>
/// <para>
/// A selector that does not implement this contract owns its own proof of authority availability; composition
/// validation then skips the build-time key check and the selector's runtime outcome remains authoritative.
/// Implementations are thread-safe.
/// </para>
/// </remarks>
public interface ISecurityAuthorityCatalog
{
    /// <summary>Determines whether exactly one authority is installed under <paramref name="key"/>.</summary>
    /// <param name="key">The nonblank authority key a security publication names.</param>
    /// <returns><see langword="true"/> when the key resolves to an installed authority.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is the default value.</exception>
    public bool Contains(ComponentKey<ISecurityAuthority> key);
}
