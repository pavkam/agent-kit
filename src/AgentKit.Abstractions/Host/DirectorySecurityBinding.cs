// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Creates canonical resource and fingerprint evidence shared by directory tools and host enforcement.</summary>
public static class DirectorySecurityBinding
{
    /// <summary>Creates the canonical protected directory resource.</summary>
    /// <param name="path">The child path, or null for the configured root.</param>
    /// <returns>The exact resource used by policy and enforcement.</returns>
    public static ProtectedResource Resource(FileSystemPath? path) =>
        new(ProtectedResourceKind.Directory, path?.Value ?? ".");

    /// <summary>Computes exact normalized request evidence for one directory observation.</summary>
    /// <param name="path">The child path, or null for the configured root.</param>
    /// <returns>An algorithm-qualified SHA-256 fingerprint.</returns>
    /// <remarks>
    /// The fingerprint binds the observed directory only. Page bounds and continuation are a projection the listing
    /// consumer applies to the complete ordered snapshot after enumeration, so they are not part of the protected effect.
    /// </remarks>
    public static InputFingerprint Fingerprint(FileSystemPath? path)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            operation = "directory-enumerate",
            path = path?.Value ?? ".",
        });
        return new InputFingerprint(
            $"sha256:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant()}");
    }
}
