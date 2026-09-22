// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Creates the canonical resource, effect, and input evidence shared by file tools and host enforcement.</summary>
public static class FileSecurityBinding
{
    /// <summary>Creates the canonical protected resource for one workspace-relative file path.</summary>
    /// <param name="path">The structurally canonical file path.</param>
    /// <returns>The exact file resource used by policy, grants, and enforcement.</returns>
    public static ProtectedResource Resource(FileSystemPath path) =>
        new(ProtectedResourceKind.File, path.Value);

    /// <summary>Creates the canonical protected resource for one logical file target.</summary>
    /// <param name="target">The logical target bound to a root and normalized path.</param>
    /// <returns>The exact file resource used by policy, grants, and enforcement.</returns>
    public static ProtectedResource Resource(FileTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return new(ProtectedResourceKind.File, $"{target.RootId.Value}/{target.Path.Value}");
    }

    /// <summary>Creates the canonical protected resource for one resolved file target.</summary>
    /// <param name="target">The resolved target bound by authorization.</param>
    /// <returns>The exact file resource used by policy, grants, and enforcement.</returns>
    public static ProtectedResource Resource(ResolvedFileTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return new(ProtectedResourceKind.File, $"{target.RootId.Value}/{target.RelativePath.Value}");
    }

    /// <summary>Computes the exact normalized input fingerprint for a file read.</summary>
    /// <param name="path">The path whose content or metadata may be observed.</param>
    /// <returns>An algorithm-qualified SHA-256 fingerprint.</returns>
    public static InputFingerprint ReadFingerprint(FileSystemPath path) => Hash(
        JsonSerializer.SerializeToUtf8Bytes(new { operation = "read", path = path.Value }));

    /// <summary>Computes the exact normalized input fingerprint for a spec file read.</summary>
    /// <param name="request">The capability-scoped read request.</param>
    /// <returns>An algorithm-qualified SHA-256 fingerprint.</returns>
    public static InputFingerprint ReadFingerprint(FileReadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Hash(
            JsonSerializer.SerializeToUtf8Bytes(new
            {
                operation = "read",
                root = request.Target.RootId.Value,
                path = request.Target.Path.Value,
                maxBytes = request.Bounds.MaxBytes,
            }));
    }

    /// <summary>Maps a spec write disposition to the exact security effect it requires.</summary>
    /// <param name="disposition">The explicit write disposition.</param>
    /// <returns>The corresponding create, replace, create-or-replace, or append effect.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="disposition"/> is undefined.</exception>
    public static SecurityEffect WriteEffect(FileWriteDisposition disposition) => disposition switch
    {
        FileWriteDisposition.CreateOnly => SecurityEffect.Create,
        FileWriteDisposition.ReplaceExisting => SecurityEffect.Replace,
        FileWriteDisposition.CreateOrReplace => SecurityEffect.CreateOrReplace,
        FileWriteDisposition.Append => SecurityEffect.Append,
        _ => throw new ArgumentOutOfRangeException(nameof(disposition), disposition, "Undefined file write disposition."),
    };

    /// <summary>Computes exact mutation evidence for one authorized spec file write.</summary>
    /// <param name="operation">The authorized write evidence.</param>
    /// <param name="payloadFingerprint">The fingerprint of the payload bytes to commit.</param>
    /// <returns>An algorithm-qualified input fingerprint.</returns>
    public static InputFingerprint WriteFingerprint(AuthorizedFileWrite operation, ContentHash payloadFingerprint)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return WriteFingerprint(
            operation.ResolvedTarget.RootId,
            operation.ResolvedTarget.RelativePath,
            operation.Disposition,
            operation.ExpectedTargetFingerprint,
            operation.DeclaredContentLength,
            operation.DeclaredContentFingerprint,
            operation.AtomicityMode,
            operation.EffectClass,
            payloadFingerprint);
    }

    /// <summary>Computes exact mutation evidence for one logical file write before grant binding.</summary>
    /// <param name="target">The logical write target.</param>
    /// <param name="disposition">The explicit write disposition.</param>
    /// <param name="expectedTargetFingerprint">The expected target fingerprint when required.</param>
    /// <param name="declaredContentLength">The declared payload length in bytes.</param>
    /// <param name="declaredContentFingerprint">The declared payload fingerprint.</param>
    /// <param name="atomicityMode">The required atomicity mode.</param>
    /// <param name="effectClass">The declared effect class.</param>
    /// <param name="payloadFingerprint">The fingerprint of the payload bytes to commit.</param>
    /// <returns>An algorithm-qualified input fingerprint.</returns>
    public static InputFingerprint WriteFingerprint(
        FileTarget target,
        FileWriteDisposition disposition,
        ContentHash? expectedTargetFingerprint,
        long declaredContentLength,
        ContentHash declaredContentFingerprint,
        FileWriteAtomicityMode atomicityMode,
        FileWriteEffectClass effectClass,
        ContentHash payloadFingerprint)
    {
        ArgumentNullException.ThrowIfNull(target);
        return WriteFingerprint(
            target.RootId,
            target.Path,
            disposition,
            expectedTargetFingerprint,
            declaredContentLength,
            declaredContentFingerprint,
            atomicityMode,
            effectClass,
            payloadFingerprint);
    }

    private static InputFingerprint WriteFingerprint(
        FileRootId rootId,
        NormalizedRelativePath relativePath,
        FileWriteDisposition disposition,
        ContentHash? expectedTargetFingerprint,
        long declaredContentLength,
        ContentHash declaredContentFingerprint,
        FileWriteAtomicityMode atomicityMode,
        FileWriteEffectClass effectClass,
        ContentHash payloadFingerprint) =>
        Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            operation = "write",
            root = rootId.Value,
            path = relativePath.Value,
            disposition = disposition.ToString(),
            expectedTargetFingerprint = expectedTargetFingerprint?.Value,
            declaredContentLength,
            declaredContentFingerprint = declaredContentFingerprint.Value,
            payloadFingerprint = payloadFingerprint.Value,
            atomicityMode = atomicityMode.ToString(),
            effectClass = effectClass.ToString(),
        }));

    /// <summary>Computes exact observation evidence for a complete bounded byte snapshot.</summary>
    /// <param name="path">The observed path.</param>
    /// <param name="maximumBytes">The complete-file byte bound.</param>
    /// <returns>An algorithm-qualified input fingerprint.</returns>
    public static InputFingerprint SnapshotFingerprint(FileSystemPath path, long maximumBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        return Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            operation = "snapshot",
            path = path.Value,
            maximumBytes,
        }));
    }

    /// <summary>Computes exact mutation evidence for a version-conditional atomic replacement.</summary>
    /// <param name="id">The mutation identity binding its derived staging resource.</param>
    /// <param name="path">The existing target path.</param>
    /// <param name="expectedContentFingerprint">The required current fingerprint.</param>
    /// <param name="content">The exact final bytes.</param>
    /// <returns>An algorithm-qualified input fingerprint.</returns>
    /// <exception cref="ArgumentException"><paramref name="content"/> is default.</exception>
    public static InputFingerprint AtomicReplaceFingerprint(
        WorkspaceMutationId id,
        FileSystemPath path,
        ContentHash expectedContentFingerprint,
        ImmutableArray<byte> content)
    {
        ArgumentException.ThrowIfDefault(content);
        return Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            operation = "atomic-replace",
            mutationId = id.ToString(),
            path = path.Value,
            expectedContentFingerprint = expectedContentFingerprint.Value,
            finalContentFingerprint = ContentFingerprint(content.AsSpan()).Value,
            finalBytes = content.Length,
            encodingPolicy = "exact-bytes-v1",
        }));
    }

    /// <summary>Creates the exact target and derived same-directory staging resources for one replacement.</summary>
    /// <param name="id">The mutation identity from which the private staging name is derived.</param>
    /// <param name="path">The existing target path.</param>
    /// <returns>The ordered target and staging resources.</returns>
    public static ImmutableArray<ProtectedResource> AtomicReplaceResources(
        WorkspaceMutationId id,
        FileSystemPath path) => [Resource(path), Resource(AtomicReplaceStagingPath(id, path))];

    /// <summary>Derives the private same-directory staging path for one mutation.</summary>
    /// <param name="id">The mutation identity.</param>
    /// <param name="path">The target path.</param>
    /// <returns>The structurally canonical staging path.</returns>
    public static FileSystemPath AtomicReplaceStagingPath(WorkspaceMutationId id, FileSystemPath path)
    {
        var separator = path.Value.LastIndexOf('/');
        var parent = separator < 0 ? "" : path.Value[..(separator + 1)];
        return new FileSystemPath($"{parent}.agentkit-stage-{id.Value:N}.tmp");
    }

    /// <summary>Computes the canonical SHA-256 fingerprint for exact content bytes.</summary>
    /// <param name="content">The exact bytes.</param>
    /// <returns>The algorithm-qualified lowercase fingerprint.</returns>
    public static ContentHash ContentFingerprint(ReadOnlySpan<byte> content) => new(
        $"sha256:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant()}");

    /// <summary>Computes the exact normalized input fingerprint for a text-file write.</summary>
    /// <param name="path">The write target.</param>
    /// <param name="content">The exact UTF-16 application text that will be encoded as UTF-8.</param>
    /// <param name="mode">The required target-state disposition.</param>
    /// <returns>An algorithm-qualified SHA-256 fingerprint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is undefined.</exception>
    public static InputFingerprint WriteFingerprint(FileSystemPath path, string content, FileWriteMode mode)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfUndefined(mode);
        return Hash(JsonSerializer.SerializeToUtf8Bytes(new { operation = "write", path = path.Value, content, mode }));
    }

    /// <summary>Maps a file disposition to the exact security effect it requires.</summary>
    /// <param name="mode">The explicit write disposition.</param>
    /// <returns>The corresponding create, create-or-replace, or append effect.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is undefined.</exception>
    public static SecurityEffect WriteEffect(FileWriteMode mode) => mode switch
    {
        FileWriteMode.CreateOrOverwrite => SecurityEffect.CreateOrReplace,
        FileWriteMode.CreateNew => SecurityEffect.Create,
        FileWriteMode.ReplaceExisting => SecurityEffect.Replace,
        FileWriteMode.Append => SecurityEffect.Append,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Undefined file write mode."),
    };

    private static InputFingerprint Hash(ReadOnlySpan<byte> bytes) =>
        new($"sha256:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant()}");
}
