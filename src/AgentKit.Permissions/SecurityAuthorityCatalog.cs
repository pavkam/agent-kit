// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>The first-party <see cref="ISecurityAuthorityCatalog"/> over the host's explicit authority key registrations.</summary>
/// <remarks>
/// The catalog reads only the immutable key markers installed by <c>AddSecurityAuthority</c>. It never activates or
/// invokes an authority, performs no I/O, and is thread-safe.
/// </remarks>
internal sealed class SecurityAuthorityCatalog: ISecurityAuthorityCatalog
{
    private readonly FrozenSet<ComponentKey<ISecurityAuthority>> _keys;

    /// <summary>Initializes the catalog from explicit key markers.</summary>
    /// <param name="registrations">The key markers installed by the host composition.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registrations"/> is null.</exception>
    public SecurityAuthorityCatalog(IEnumerable<SecurityAuthorityKeyRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        _keys = registrations.Select(static registration => registration.Key).ToFrozenSet();
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="key"/> is the default value.</exception>
    public bool Contains(ComponentKey<ISecurityAuthority> key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        return _keys.Contains(key);
    }
}
