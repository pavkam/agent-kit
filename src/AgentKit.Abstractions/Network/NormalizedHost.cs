// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;
using System.Net;

/// <summary>
/// A DNS hostname or IP-address literal canonicalized to lowercase,
/// trimmed text.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>. It carries no mutable state
/// itself and is safe to share, compare, and use as a lookup key across
/// threads without synchronization.
/// </para>
/// <para>
/// Canonicalizing case here — rather than leaving comparison
/// case-sensitivity to every caller — is deliberate: hostnames are
/// case-insensitive per RFC 4343, and a destination-policy allow list that
/// compared case-sensitively could be trivially bypassed by an attacker
/// varying letter case. This type never resolves the name; resolution and
/// address-level policy remain the responsibility of
/// <see cref="INetworkNameResolver"/>.
/// </para>
/// </remarks>
public readonly record struct NormalizedHost
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NormalizedHost"/>
    /// struct, validating and canonicalizing the supplied host text.
    /// </summary>
    /// <param name="value">The non-empty hostname or IP-address literal text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, consists only of
    /// whitespace, or is not a canonicalizable DNS host (for example, an
    /// undecodable Punycode label).
    /// </exception>
    public NormalizedHost(string value)
    {
        ArgumentException.ThrowIfInvalidNetworkHost(value);
        var candidate = value.Trim().TrimEnd('.');
        if (IPAddress.TryParse(candidate, out var address))
        {
            Value = address.ToString().ToLowerInvariant();
            return;
        }

        try
        {
            Value = new IdnMapping().GetAscii(candidate).ToLowerInvariant();
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException("Value must be a canonicalizable DNS host.", nameof(value), exception);
        }
    }

    /// <summary>Gets the canonicalized, lowercase host text.</summary>
    public string Value { get; }

    /// <summary>
    /// Returns the canonicalized host text, suitable for logging and
    /// diagnostic messages.
    /// </summary>
    public override string ToString() => Value;
}
