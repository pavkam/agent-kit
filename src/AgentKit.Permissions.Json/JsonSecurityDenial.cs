// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json;

/// <summary>Portable JSON mirror of <see cref="SecurityDenial"/>.</summary>
/// <param name="Code">The stable machine-readable denial code.</param>
/// <param name="SafeMessage">The non-sensitive caller-facing explanation.</param>
public sealed record JsonSecurityDenial(string Code, string SafeMessage)
{
    /// <summary>Projects one domain denial into its portable JSON representation.</summary>
    /// <param name="value">The non-null denial to project.</param>
    /// <returns>A document carrying the exact code and safe message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static JsonSecurityDenial FromDomain(SecurityDenial value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new JsonSecurityDenial(value.Code, value.SafeMessage);
    }

    /// <summary>Reconstructs the exact domain denial this document was projected from.</summary>
    /// <returns>A denial equal to the projected original.</returns>
    /// <exception cref="ArgumentException">Either persisted field is blank.</exception>
    public SecurityDenial ToDomain() => new(Code, SafeMessage);
}
