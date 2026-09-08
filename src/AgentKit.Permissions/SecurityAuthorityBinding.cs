// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Binds one explicit authority key to the host-owned singleton that implements it.</summary>
/// <remarks>
/// This DI composition value is created during registration, so runtime selection never searches a
/// service provider or substitutes an unkeyed authority. The host retains ownership of the supplied
/// singleton instance and is responsible for disposing it when appropriate.
/// </remarks>
internal sealed record SecurityAuthorityBinding
{
    /// <summary>Initializes one explicit authority composition binding.</summary>
    /// <param name="key">The non-empty key captured into authorization contexts.</param>
    /// <param name="authority">The host-owned authority selected only for <paramref name="key"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is default.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="authority"/> is null.</exception>
    public SecurityAuthorityBinding(ComponentKey<ISecurityAuthority> key, ISecurityAuthority authority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(authority);
        Key = key;
        Authority = authority;
    }

    /// <summary>Gets the exact selection key associated with the authority.</summary>
    /// <value>A non-default immutable authority component key.</value>
    public ComponentKey<ISecurityAuthority> Key { get; }

    /// <summary>Gets the host-owned singleton selected by <see cref="Key"/>.</summary>
    /// <value>A non-null authority instance; this binding does not transfer its lifetime ownership.</value>
    public ISecurityAuthority Authority { get; }
}
