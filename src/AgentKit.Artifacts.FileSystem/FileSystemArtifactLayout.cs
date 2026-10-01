// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.FileSystem;

using System.Security.Cryptography;
using System.Text;

/// <summary>Names every file a file-system artifact store owns directly under its root.</summary>
/// <remarks>
/// The layout is flat because the store creates no directories, so enumerating the root lists every file the store owns. Payload names combine the SHA-256 of the tenant and the content
/// hash, so identical bytes in two tenants are two files and no name reveals another tenant's identity or content.
/// </remarks>
internal static class FileSystemArtifactLayout
{
    /// <summary>Gets the name of the newline-delimited entry log.</summary>
    internal const string LogName = "artifacts.jsonl";

    private const string _payloadPrefix = "payload-";
    private const string _payloadSuffix = ".bin";

    private static readonly System.Buffers.SearchValues<char> _lowerHex = System.Buffers.SearchValues.Create("0123456789abcdef");

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
            ? $"{_payloadPrefix}{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(tenantId.Value)))}-{digest}{_payloadSuffix}"
            : throw new InvalidDataException("A content hash cannot name a payload file.");
    }

    /// <summary>Determines whether a root entry name is shaped like a payload file this store names.</summary>
    /// <param name="name">The single-segment entry name observed directly under the root.</param>
    /// <returns><see langword="true"/> when the name is a payload prefix, a 64-character lowercase tenant digest, a separator, a nonempty lowercase hexadecimal content digest, and the payload suffix; the entry log and every foreign file are never payload names.</returns>
    /// <remarks>Only names this predicate accepts are ever swept, so a file the store did not name is never deleted.</remarks>
    internal static bool IsPayloadName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        const int tenantDigestLength = 64;
        if (!name.StartsWith(_payloadPrefix, StringComparison.Ordinal) || !name.EndsWith(_payloadSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        var body = name.AsSpan(_payloadPrefix.Length, name.Length - _payloadPrefix.Length - _payloadSuffix.Length);
        return body.Length > tenantDigestLength + 1
            && body[tenantDigestLength] == '-'
            && body[..tenantDigestLength].IndexOfAnyExcept(_lowerHex) < 0
            && body[(tenantDigestLength + 1)..].IndexOfAnyExcept(_lowerHex) < 0;
    }
}
