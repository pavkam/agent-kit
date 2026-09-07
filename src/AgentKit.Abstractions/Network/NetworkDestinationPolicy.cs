// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Net;
using System.Net.Sockets;

/// <summary>
/// A structural allow/deny policy over destination schemes, hosts, and
/// resolved address ranges.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </para>
/// <para>
/// This is a deliberately reduced stand-in for the fuller security-request
/// and grant model the full network architecture describes, which
/// additionally binds authority to a canonical destination, data
/// classification, and request fingerprint through a shared security
/// authority. Until that authority is wired into this call site, this
/// policy is the equivalent of the file-system boundary's own structural
/// sandbox check: an independent, always-enforced second check that a
/// higher-level allow cannot be used to reach a destination outside the
/// configured policy. It is never a substitute for that separate
/// authorization decision, only a narrower guarantee this package can make
/// on its own.
/// </para>
/// </remarks>
public sealed record NetworkDestinationPolicy
{
    /// <summary>Gets a permissive default policy: any scheme, any host, private and loopback addresses excluded.</summary>
    public static NetworkDestinationPolicy Default { get; } = new(["https"], null, allowPrivateAddresses: false);

    /// <summary>Initializes a new instance of the <see cref="NetworkDestinationPolicy"/> record.</summary>
    /// <param name="allowedSchemes">The non-empty set of URI schemes this policy permits.</param>
    /// <param name="allowedHosts">
    /// The exact set of hosts this policy permits, when restricted;
    /// <see langword="null"/> permits any host subject to the other checks
    /// this policy declares.
    /// </param>
    /// <param name="allowPrivateAddresses">
    /// Whether a resolved address in a private, loopback, or link-local
    /// range is permitted.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="allowedSchemes"/> is a default, uninitialized array, or is empty, or
    /// <paramref name="allowedHosts"/> is a default, uninitialized (but non-null) array.
    /// </exception>
    public NetworkDestinationPolicy(
        ImmutableArray<string> allowedSchemes,
        ImmutableArray<NormalizedHost>? allowedHosts,
        bool allowPrivateAddresses)
    {
        ArgumentException.ThrowIfDefault(allowedSchemes);
        if (allowedSchemes.IsEmpty)
        {
            throw new ArgumentException("At least one allowed scheme is required.", nameof(allowedSchemes));
        }

        if (allowedHosts is { IsDefault: true })
        {
            throw new ArgumentException(
                "Value must not be a default ImmutableArray<T>. Use ImmutableArray<T>.Empty or null.",
                nameof(allowedHosts));
        }

        AllowedSchemes = [.. allowedSchemes.Select(static scheme => scheme.ToLowerInvariant())];
        AllowedHosts = allowedHosts;
        AllowPrivateAddresses = allowPrivateAddresses;
    }

    /// <summary>Gets the set of URI schemes this policy permits, in lowercase.</summary>
    public ImmutableArray<string> AllowedSchemes { get; init; }

    /// <summary>
    /// Gets the exact set of hosts this policy permits, when restricted;
    /// <see langword="null"/> permits any host subject to the other checks
    /// this policy declares.
    /// </summary>
    public ImmutableArray<NormalizedHost>? AllowedHosts { get; init; }

    /// <summary>
    /// Gets a value indicating whether a resolved address in a private,
    /// loopback, or link-local range is permitted.
    /// </summary>
    public bool AllowPrivateAddresses { get; init; }

    /// <summary>
    /// Determines whether this policy permits <paramref name="destination"/>'s
    /// scheme and host, independent of any address it might resolve to.
    /// </summary>
    /// <param name="destination">The destination to evaluate.</param>
    /// <returns><see langword="true"/> if the scheme and host are permitted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is null.</exception>
    public bool AllowsSchemeAndHost(NetworkDestination destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        return AllowedSchemes.Contains(destination.Scheme)
            && (AllowedHosts is not { } allowedHosts || allowedHosts.Contains(destination.Host));
    }

    /// <summary>Determines whether this policy permits connecting to <paramref name="address"/>.</summary>
    /// <param name="address">The resolved address to evaluate.</param>
    /// <returns><see langword="true"/> if the address is permitted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is null.</exception>
    public bool AllowsAddress(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return AllowPrivateAddresses || !IsPrivateOrLoopback(address);
    }

    private static bool IsPrivateOrLoopback(IPAddress address) =>
        IPAddress.IsLoopback(address)
        || address.IsIPv6LinkLocal
        || address.IsIPv6SiteLocal
        || address.IsIPv6UniqueLocal
        || (address.AddressFamily == AddressFamily.InterNetwork && IsPrivateIPv4(address.GetAddressBytes()));

    private static bool IsPrivateIPv4(byte[] octets) => octets[0] switch
    {
        10 => true,
        127 => true,
        169 when octets[1] == 254 => true,
        172 when octets[1] is >= 16 and <= 31 => true,
        192 when octets[1] == 168 => true,
        0 => true,
        _ => false,
    };

    /// <inheritdoc/>
    public bool Equals(NetworkDestinationPolicy? other) =>
        other is not null
        && AllowedSchemes.SequenceEqual(other.AllowedSchemes)
        && NullableSequenceEqual(AllowedHosts, other.AllowedHosts)
        && AllowPrivateAddresses == other.AllowPrivateAddresses;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var scheme in AllowedSchemes)
        {
            hash.Add(scheme);
        }

        if (AllowedHosts is { } hosts)
        {
            foreach (var host in hosts)
            {
                hash.Add(host);
            }
        }

        hash.Add(AllowPrivateAddresses);
        return hash.ToHashCode();
    }

    private static bool NullableSequenceEqual(ImmutableArray<NormalizedHost>? left, ImmutableArray<NormalizedHost>? right) =>
        left is null || right is null
            ? left is null && right is null
            : left.Value.SequenceEqual(right.Value);
}
