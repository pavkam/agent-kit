// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Builds logical and resolved file targets from configured roots without host I/O.</summary>
public static class FileHostTargetBinding
{
    /// <summary>Combines one host root directory with a normalized relative path.</summary>
    /// <param name="rootId">The configured root identifier.</param>
    /// <param name="relativePath">The normalized path relative to <paramref name="rootId"/>.</param>
    /// <param name="hostRootPath">The absolute host directory for the root.</param>
    /// <returns>A resolved target suitable for authorization and host enforcement.</returns>
    /// <exception cref="ArgumentException"><paramref name="hostRootPath"/> is null, empty, or whitespace.</exception>
    public static ResolvedFileTarget Resolve(
        FileRootId rootId,
        NormalizedRelativePath relativePath,
        string hostRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostRootPath);
        var hostTargetPath = Path.GetFullPath(Path.Combine(hostRootPath, relativePath.Value));
        var targetFingerprint = FileSecurityBinding.ContentFingerprint(
            System.Text.Encoding.UTF8.GetBytes($"{rootId.Value}/{relativePath.Value}"));
        return new ResolvedFileTarget(
            rootId,
            relativePath,
            hostTargetPath,
            FilePathComparisonKind.Ordinal,
            FileSecurityBinding.ContentFingerprint("no-link"u8),
            targetFingerprint);
    }

    /// <summary>Creates the logical target for security requests.</summary>
    /// <param name="rootId">The configured root identifier.</param>
    /// <param name="relativePath">The normalized relative path.</param>
    /// <returns>A logical file target.</returns>
    public static FileTarget Target(FileRootId rootId, NormalizedRelativePath relativePath) => new(rootId, relativePath);
}
