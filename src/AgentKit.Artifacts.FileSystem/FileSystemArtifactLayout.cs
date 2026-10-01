// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.FileSystem;

using System.Security.Cryptography;
using System.Text;

/// <summary>Names every file a file-system artifact store owns directly under its root.</summary>
/// <remarks>
/// The layout is flat because the store creates no directories. Payload names combine the SHA-256 of the tenant and the content
/// hash, so identical bytes in two tenants are two files and no name reveals another tenant's identity or content.
/// </remarks>
internal static class FileSystemArtifactLayout
{
    /// <summary>Gets the name of the newline-delimited entry log.</summary>
    internal const string LogName = "artifacts.jsonl";

    private const string _payloadPrefix = "payload-";

    /// <summary>Names the payload file for one tenant's content.</summary>
    /// <param name="tenantId">The tenant partition.</param>
    /// <param name="contentHash">The verified content hash, in algorithm-qualified lowercase hexadecimal form.</param>
    /// <returns>The single-segment payload file name.</returns>
    /// <exception cref="ArgumentException"><paramref name="tenantId"/> is blank.</exception>
    /// <exception cref="InvalidDataException">The hash is not lowercase hexadecimal and so cannot name a file safely.</exception>
    internal static string PayloadName(TenantId tenantId, ContentHash contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        var value = contentHash.Value;
        var separator = value.IndexOf(':', StringComparison.Ordinal);
        var digest = separator >= 0 ? value[(separator + 1)..] : value;
        return digest.Length > 0 && digest.All(static character => char.IsAsciiHexDigitLower(character))
            ? $"{_payloadPrefix}{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(tenantId.Value)))}-{digest}.bin"
            : throw new InvalidDataException("A content hash cannot name a payload file.");
    }
}
