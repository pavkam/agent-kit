// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Creates exact per-entry authorization evidence for workspace patch effects.</summary>
public static class WorkspacePatchSecurityBinding
{
    /// <summary>Creates target and private staging resources for a create entry.</summary>
    /// <param name="id">The mutation identity.</param>
    /// <param name="path">The absent target.</param>
    /// <returns>The ordered target and staging resources.</returns>
    public static ImmutableArray<ProtectedResource> CreateResources(
        WorkspaceMutationId id,
        FileSystemPath path) => FileSecurityBinding.AtomicReplaceResources(id, path);

    /// <summary>Computes exact create evidence over target, staging identity, and final bytes.</summary>
    /// <param name="id">The mutation identity.</param>
    /// <param name="path">The absent target.</param>
    /// <param name="content">The exact final bytes.</param>
    /// <returns>The canonical input fingerprint.</returns>
    /// <exception cref="ArgumentException"><paramref name="content"/> is default.</exception>
    public static InputFingerprint CreateFingerprint(
        WorkspaceMutationId id,
        FileSystemPath path,
        ImmutableArray<byte> content)
    {
        ArgumentException.ThrowIfDefault(content);
        return Hash(new
        {
            operation = "patch-create",
            mutationId = id.ToString(),
            path = path.Value,
            finalContentFingerprint = FileSecurityBinding.ContentFingerprint(content.AsSpan()).Value,
            finalBytes = content.Length,
            createModePolicy = "owner-read-write-v1",
            encodingPolicy = "exact-bytes-v1",
        });
    }

    /// <summary>Creates the exact target resource for a delete entry.</summary>
    /// <param name="path">The existing target.</param>
    /// <returns>The one target resource.</returns>
    public static ImmutableArray<ProtectedResource> DeleteResources(FileSystemPath path) =>
        [FileSecurityBinding.Resource(path)];

    /// <summary>Computes exact delete evidence over the target and required current version.</summary>
    /// <param name="path">The existing target.</param>
    /// <param name="expectedContentFingerprint">The required current fingerprint.</param>
    /// <returns>The canonical input fingerprint.</returns>
    public static InputFingerprint DeleteFingerprint(
        FileSystemPath path,
        ContentHash expectedContentFingerprint) => Hash(new
        {
            operation = "patch-delete",
            path = path.Value,
            expectedContentFingerprint = expectedContentFingerprint.Value,
        });

    /// <summary>Creates ordered source and destination resources for a move entry.</summary>
    /// <param name="sourcePath">The existing source.</param>
    /// <param name="destinationPath">The absent destination.</param>
    /// <returns>The source and destination resources.</returns>
    public static ImmutableArray<ProtectedResource> MoveResources(
        FileSystemPath sourcePath,
        FileSystemPath destinationPath) =>
        [FileSecurityBinding.Resource(sourcePath), FileSecurityBinding.Resource(destinationPath)];

    /// <summary>Computes exact move evidence over both paths and the required source version.</summary>
    /// <param name="sourcePath">The existing source.</param>
    /// <param name="destinationPath">The absent destination.</param>
    /// <param name="expectedContentFingerprint">The required current source fingerprint.</param>
    /// <returns>The canonical input fingerprint.</returns>
    public static InputFingerprint MoveFingerprint(
        FileSystemPath sourcePath,
        FileSystemPath destinationPath,
        ContentHash expectedContentFingerprint) => Hash(new
        {
            operation = "patch-move",
            sourcePath = sourcePath.Value,
            destinationPath = destinationPath.Value,
            expectedContentFingerprint = expectedContentFingerprint.Value,
        });

    private static InputFingerprint Hash<T>(T value) => new(
        $"sha256:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(value))).ToLowerInvariant()}");
}
