// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Computes the canonical private binding retained for one exact enforcement intent.</summary>
public static class SecurityEnforcementBinding
{
    /// <summary>Fingerprints complete recomputed enforcement evidence and its stable intent.</summary><param name="enforcement">The non-null concrete effect evidence.</param><param name="intent">The non-null attempt identity and fence.</param><returns>An algorithm-qualified canonical digest.</returns><exception cref="ArgumentNullException">A parameter is null.</exception>
    public static ContentHash Fingerprint(SecurityEnforcementRequest enforcement, SecurityEnforcementIntent intent)
    {
        ArgumentNullException.ThrowIfNull(enforcement);
        ArgumentNullException.ThrowIfNull(intent);
        return new ContentHash(SecurityCanonicalFingerprint.Create(
            new SecurityEnforcementFingerprintPayload(enforcement, intent)).Value);
    }
}
