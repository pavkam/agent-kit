// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

/// <summary>Owns one isolated temporary directory root and the fixed identity every reopen of it must present.</summary>
/// <remarks>
/// Reopening is the only way to observe the directory's replay behavior, and it requires the same path and the same
/// persistent instance identity each time. Holding both here keeps a case from accidentally proving durability against a
/// freshly created second root.
/// </remarks>
internal sealed class TestDirectoryRoot: IDisposable
{
    private readonly string _path = TestTemporaryDirectory.Create();

    /// <summary>Gets the fully qualified directory root this instance owns.</summary>
    internal string DirectoryPath => Path.Combine(_path, "directory");

    /// <summary>Gets the fixed persistent directory identity every open of this root must present.</summary>
    internal JsonSessionDirectoryInstanceId InstanceId { get; } = new(Guid.NewGuid());

    /// <summary>Composes one directory bound to this root without initializing it.</summary>
    /// <param name="auditDispatcher">The required audit dispatcher.</param>
    /// <param name="grantStore">The authoritative grant store that validates and consumes each directory grant.</param>
    /// <param name="auditRecordIds">The deterministic audit-record identity source.</param>
    /// <param name="timeProvider">The controllable clock used for audit timestamps.</param>
    /// <param name="settings">The settings to bind; defaults to <see cref="JsonSessionDirectorySettings.CreateDefault"/>.</param>
    /// <param name="openMode">Whether the root may be created.</param>
    /// <param name="recoveryMode">Whether a torn trailing append may be discarded.</param>
    /// <param name="instanceId">Overrides the expected persistent identity; defaults to <see cref="InstanceId"/>.</param>
    /// <param name="logger">The optional content-free diagnostic logger.</param>
    /// <returns>A new directory the caller owns and must dispose before opening another over this root.</returns>
    /// <exception cref="ArgumentNullException">A collaborator is null.</exception>
    /// <remarks>
    /// The returned directory holds the root's advisory exclusive lock only once initialized, so a case must dispose one
    /// instance before opening the next.
    /// </remarks>
    internal JsonSessionDirectory Open(
        ISecurityAuditDispatcher auditDispatcher,
        ISecurityGrantStore grantStore,
        IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
        TimeProvider timeProvider,
        JsonSessionDirectorySettings? settings = null,
        JsonStoreOpenMode openMode = JsonStoreOpenMode.CreateIfMissing,
        JsonStoreRecoveryMode recoveryMode = JsonStoreRecoveryMode.RecoverTornAppends,
        JsonSessionDirectoryInstanceId? instanceId = null,
        Microsoft.Extensions.Logging.ILogger<JsonSessionDirectory>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(auditDispatcher);
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(auditRecordIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        return new JsonSessionDirectory(
            new ComponentId("agentkit.session.directory.json"),
            auditDispatcher,
            grantStore,
            auditRecordIds,
            timeProvider,
            new JsonSessionDirectoryTarget(DirectoryPath, instanceId ?? InstanceId, openMode, recoveryMode),
            settings ?? JsonSessionDirectorySettings.CreateDefault(),
            logger);
    }

    /// <summary>Removes the temporary root and everything the case wrote into it.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_path))
        {
            Directory.Delete(_path, recursive: true);
        }
    }
}
