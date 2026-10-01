// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Json;

/// <summary>Names the one host-authorized directory a JSON memory, document, or vector store owns and how it may be opened.</summary>
/// <remarks>The path is sensitive bootstrap configuration and is never logged. Each store needs its own root: the advisory lock admits one writer per root. Creating a missing root while also requiring exact validation is contradictory and is refused.</remarks>
public sealed record JsonMemoryTarget
{
    /// <summary>Initializes a validated target.</summary>
    /// <param name="directoryPath">The fully qualified store root directory.</param>
    /// <param name="expectedInstanceId">The identity the root's manifest must carry or be created with.</param>
    /// <param name="openMode">Whether a missing root may be created.</param>
    /// <param name="recoveryMode">Whether a torn trailing append is recovered or refused.</param>
    /// <exception cref="ArgumentNullException"><paramref name="directoryPath"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="directoryPath"/> is blank or not fully qualified.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The instance identity is empty, a mode is undefined, or creation is combined with exact validation.</exception>
    public JsonMemoryTarget(
        string directoryPath,
        JsonMemoryInstanceId expectedInstanceId,
        JsonStoreOpenMode openMode,
        JsonStoreRecoveryMode recoveryMode)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentOutOfRangeException.ThrowIfEqual(expectedInstanceId, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(openMode);
        ArgumentOutOfRangeException.ThrowIfUndefined(recoveryMode);
        ArgumentOutOfRangeException.ThrowIfEqual(
            (openMode, recoveryMode),
            (JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.ValidateExact),
            nameof(recoveryMode));
        if (!Path.IsPathFullyQualified(directoryPath))
        {
            throw new ArgumentException("The JSON memory-store root must be a fully qualified path.", nameof(directoryPath));
        }

        DirectoryPath = Path.GetFullPath(directoryPath);
        ExpectedInstanceId = expectedInstanceId;
        OpenMode = openMode;
        RecoveryMode = recoveryMode;
    }

    /// <summary>Gets the normalized store root.</summary>
    public string DirectoryPath { get; }

    /// <summary>Gets the instance identity the manifest must carry.</summary>
    public JsonMemoryInstanceId ExpectedInstanceId { get; }

    /// <summary>Gets whether a missing root may be created.</summary>
    public JsonStoreOpenMode OpenMode { get; }

    /// <summary>Gets whether a torn trailing append is recovered or refused.</summary>
    public JsonStoreRecoveryMode RecoveryMode { get; }
}
