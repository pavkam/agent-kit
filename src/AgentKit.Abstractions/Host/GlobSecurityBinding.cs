// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Creates canonical resource and input evidence shared by glob tools and host enforcement.</summary>
public static class GlobSecurityBinding
{
    /// <summary>Creates the recursively observed directory resource.</summary>
    /// <param name="basePath">The traversal base, or null for root.</param>
    /// <returns>The canonical directory resource.</returns>
    public static ProtectedResource Resource(FileSystemPath? basePath) => DirectorySecurityBinding.Resource(basePath);

    /// <summary>Computes an exact fingerprint over glob dialect inputs and all traversal bounds.</summary>
    /// <param name="basePath">The traversal base, or null for root.</param>
    /// <param name="pattern">The pinned simple pattern.</param>
    /// <param name="caseSensitive">Whether matching is case-sensitive.</param>
    /// <param name="includeHidden">Whether dot-prefixed names are visited.</param>
    /// <param name="maximumDepth">The recursive depth bound.</param>
    /// <param name="maximumVisitedEntries">The visit bound.</param>
    /// <param name="maximumResults">The result bound.</param>
    /// <returns>An algorithm-qualified SHA-256 fingerprint.</returns>
    public static InputFingerprint Fingerprint(
        FileSystemPath? basePath,
        GlobPattern pattern,
        bool caseSensitive,
        bool includeHidden,
        int maximumDepth,
        int maximumVisitedEntries,
        int maximumResults) => FingerprintCore(
        basePath,
        pattern,
        caseSensitive,
        includeHidden,
        maximumDepth,
        maximumVisitedEntries,
        maximumResults);

    /// <summary>Computes an exact fingerprint including explicit traversal exclusions.</summary>
    /// <param name="basePath">The traversal base, or null for root.</param>
    /// <param name="pattern">The pinned simple pattern.</param>
    /// <param name="caseSensitive">Whether matching is case-sensitive.</param>
    /// <param name="includeHidden">Whether dot-prefixed names are visited.</param>
    /// <param name="maximumDepth">The recursive depth bound.</param>
    /// <param name="maximumVisitedEntries">The visit bound.</param>
    /// <param name="maximumResults">The result bound.</param>
    /// <param name="excludedPathPatterns">The ordered traversal exclusions.</param>
    /// <returns>An algorithm-qualified SHA-256 fingerprint.</returns>
    public static InputFingerprint Fingerprint(
        FileSystemPath? basePath,
        GlobPattern pattern,
        bool caseSensitive,
        bool includeHidden,
        int maximumDepth,
        int maximumVisitedEntries,
        int maximumResults,
        ImmutableArray<GlobPattern> excludedPathPatterns = default)
    {
        return excludedPathPatterns.IsDefaultOrEmpty
            ? FingerprintCore(
                basePath, pattern, caseSensitive, includeHidden, maximumDepth, maximumVisitedEntries, maximumResults)
            : FingerprintCore(
                basePath,
                pattern,
                caseSensitive,
                includeHidden,
                maximumDepth,
                maximumVisitedEntries,
                maximumResults,
                excludedPathPatterns);
    }

    private static InputFingerprint FingerprintCore(
        FileSystemPath? basePath,
        GlobPattern pattern,
        bool caseSensitive,
        bool includeHidden,
        int maximumDepth,
        int maximumVisitedEntries,
        int maximumResults,
        ImmutableArray<GlobPattern> excludedPathPatterns = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumVisitedEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResults);
        var bytes = excludedPathPatterns.IsDefaultOrEmpty
            ? JsonSerializer.SerializeToUtf8Bytes(new
            {
                operation = "glob",
                basePath = basePath?.Value ?? ".",
                dialect = "agentkit-simple-glob-v1",
                pattern = pattern.Value,
                caseSensitive,
                includeHidden,
                maximumDepth,
                maximumVisitedEntries,
                maximumResults,
            })
            : JsonSerializer.SerializeToUtf8Bytes(new
            {
                operation = "glob",
                basePath = basePath?.Value ?? ".",
                dialect = "agentkit-simple-glob-v1",
                pattern = pattern.Value,
                caseSensitive,
                includeHidden,
                maximumDepth,
                maximumVisitedEntries,
                maximumResults,
                excludedPathPatterns = excludedPathPatterns.Select(static value => value.Value),
            });
        return new InputFingerprint(
            $"sha256:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant()}");
    }
}
