// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Creates exact resource and input evidence shared by search tools and host enforcement.</summary>
public static class FileSearchSecurityBinding
{
    /// <summary>Creates the recursively observed directory resource.</summary>
    /// <param name="basePath">The traversal base, or null for root.</param>
    /// <returns>The canonical directory resource.</returns>
    public static ProtectedResource Resource(FileSystemPath? basePath) => DirectorySecurityBinding.Resource(basePath);

    /// <summary>Computes an exact fingerprint over the engine profile and every visibility or resource bound.</summary>
    /// <param name="basePath">The traversal base.</param>
    /// <param name="pattern">The content pattern.</param>
    /// <param name="pathPattern">The candidate-path glob.</param>
    /// <param name="caseSensitive">Whether content matching is case-sensitive.</param>
    /// <param name="includeHidden">Whether hidden names are visited.</param>
    /// <param name="maximumDepth">The traversal-depth bound.</param>
    /// <param name="maximumFiles">The candidate-file bound.</param>
    /// <param name="maximumBytes">The observed-byte bound.</param>
    /// <param name="maximumMatches">The retained-match bound.</param>
    /// <param name="maximumLineBytes">The line-projection bound.</param>
    /// <param name="maximumDuration">The elapsed-time bound.</param>
    /// <returns>An algorithm-qualified SHA-256 fingerprint.</returns>
    public static InputFingerprint Fingerprint(
        FileSystemPath? basePath,
        FileSearchPattern pattern,
        GlobPattern pathPattern,
        bool caseSensitive,
        bool includeHidden,
        int maximumDepth,
        int maximumFiles,
        long maximumBytes,
        int maximumMatches,
        int maximumLineBytes,
        TimeSpan maximumDuration) => FingerprintCore(
        basePath,
        pattern,
        pathPattern,
        caseSensitive,
        includeHidden,
        maximumDepth,
        maximumFiles,
        maximumBytes,
        maximumMatches,
        maximumLineBytes,
        maximumDuration);

    /// <summary>Computes an exact fingerprint including explicit traversal exclusions.</summary>
    /// <param name="basePath">The traversal base.</param>
    /// <param name="pattern">The content pattern.</param>
    /// <param name="pathPattern">The candidate-path glob.</param>
    /// <param name="caseSensitive">Whether content matching is case-sensitive.</param>
    /// <param name="includeHidden">Whether hidden names are visited.</param>
    /// <param name="maximumDepth">The traversal-depth bound.</param>
    /// <param name="maximumFiles">The candidate-file bound.</param>
    /// <param name="maximumBytes">The observed-byte bound.</param>
    /// <param name="maximumMatches">The retained-match bound.</param>
    /// <param name="maximumLineBytes">The line-projection bound.</param>
    /// <param name="maximumDuration">The elapsed-time bound.</param>
    /// <param name="excludedPathPatterns">The ordered traversal exclusions.</param>
    /// <returns>An algorithm-qualified SHA-256 fingerprint.</returns>
    public static InputFingerprint Fingerprint(
        FileSystemPath? basePath,
        FileSearchPattern pattern,
        GlobPattern pathPattern,
        bool caseSensitive,
        bool includeHidden,
        int maximumDepth,
        int maximumFiles,
        long maximumBytes,
        int maximumMatches,
        int maximumLineBytes,
        TimeSpan maximumDuration,
        ImmutableArray<GlobPattern> excludedPathPatterns = default)
    {
        return excludedPathPatterns.IsDefaultOrEmpty
            ? FingerprintCore(
                basePath, pattern, pathPattern, caseSensitive, includeHidden, maximumDepth, maximumFiles, maximumBytes,
                maximumMatches, maximumLineBytes, maximumDuration)
            : FingerprintCore(
                basePath, pattern, pathPattern, caseSensitive, includeHidden, maximumDepth, maximumFiles, maximumBytes,
                maximumMatches, maximumLineBytes, maximumDuration, excludedPathPatterns);
    }

    private static InputFingerprint FingerprintCore(
        FileSystemPath? basePath,
        FileSearchPattern pattern,
        GlobPattern pathPattern,
        bool caseSensitive,
        bool includeHidden,
        int maximumDepth,
        int maximumFiles,
        long maximumBytes,
        int maximumMatches,
        int maximumLineBytes,
        TimeSpan maximumDuration,
        ImmutableArray<GlobPattern> excludedPathPatterns = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumFiles);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumMatches);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumLineBytes);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumDuration, TimeSpan.Zero);
        var common = new
        {
            operation = "file-search",
            basePath = basePath?.Value ?? ".",
            engine = pattern.Kind == FileSearchPatternKind.Literal
                ? "ordinal-literal-v1"
                : "dotnet-nonbacktracking-regex-v1",
            pattern = pattern.Value,
            pathDialect = "agentkit-simple-glob-v1",
            pathPattern = pathPattern.Value,
            caseSensitive,
            includeHidden,
            binaryPolicy = "exclude-nul-or-invalid-utf8-v1",
            maximumDepth,
            maximumFiles,
            maximumBytes,
            maximumMatches,
            maximumLineBytes,
            maximumDurationTicks = maximumDuration.Ticks,
        };
        var bytes = excludedPathPatterns.IsDefaultOrEmpty
            ? JsonSerializer.SerializeToUtf8Bytes(common)
            : JsonSerializer.SerializeToUtf8Bytes(new
            {
                common.operation,
                common.basePath,
                common.engine,
                common.pattern,
                common.pathDialect,
                common.pathPattern,
                common.caseSensitive,
                common.includeHidden,
                common.binaryPolicy,
                common.maximumDepth,
                common.maximumFiles,
                common.maximumBytes,
                common.maximumMatches,
                common.maximumLineBytes,
                common.maximumDurationTicks,
                excludedPathPatterns = excludedPathPatterns.Select(static value => value.Value),
            });
        return new InputFingerprint(
            $"sha256:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant()}");
    }
}
