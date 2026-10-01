// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.FileSystem;

/// <summary>Persists artifact entries and tenant-partitioned content-addressed payloads through the protected file-system contracts.</summary>
/// <remarks>
/// <para>
/// A plan is committed in a fixed order: the payload is written with create-or-replace, one log record holding every entry of the
/// plan is appended, then a released payload is deleted. A crash between steps can leave an unreferenced file but never a committed
/// entry without bytes or readable bytes without an entry. A torn trailing log record found during recovery is repaired by atomically
/// replacing the log with one snapshot record per entry.
/// </para>
/// <para>
/// A released payload is removed with an explicit authorized delete effect, and recovery sweeps every unreferenced payload file a
/// crash left behind by enumerating the root through the directory-reader capability and deleting only names this store derives for
/// payloads that no live entry references; the entry log and any foreign file are never touched. An altered payload fails the
/// integrity check and is reported unavailable. Payload names are derived from the SHA-256 of the tenant and the content hash, so
/// identical bytes in two tenants are two files and neither is observable through the other. The selected profile must therefore
/// declare the read, write, enumerate, and delete capabilities.
/// </para>
/// </remarks>
internal sealed class FileSystemArtifactBackend: ArtifactStateBackend
{
    private const string _logName = FileSystemArtifactLayout.LogName;
    private const byte _newLine = (byte) '\n';

    private static readonly JsonSerializerOptions _json = JsonStoreSerialization.CreateCanonicalOptions();

    private readonly FileSystemArtifactEffects _effects;
    private readonly FileSystemArtifactSettings _settings;

    internal FileSystemArtifactBackend(FileSystemArtifactEffects effects, FileSystemArtifactSettings settings, ILogger? logger)
        : base("file_system", logger)
    {
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(settings);
        _effects = effects;
        _settings = settings;
    }

    /// <inheritdoc/>
    protected override async ValueTask RecoverAsync(SecurityAuthorizationContext? authorization, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new InvalidOperationException("A file-system artifact store reads its log under the authorization of its first operation and cannot be initialized without one.");
        }

        var content = await _effects.ReadAsync(authorization, _logName, _settings.MaximumLogBytes, cancellationToken).ConfigureAwait(false);
        if (content is not null)
        {
            await ReplayAsync(authorization, content, cancellationToken).ConfigureAwait(false);
        }

        await SweepUnreferencedPayloadsAsync(authorization, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask StagePayloadAsync(SecurityAuthorizationContext authorization, ArtifactEntry entry, ImmutableArray<byte> content, CancellationToken cancellationToken)
    {
        var result = await _effects.WriteAsync(authorization, PayloadName(entry), content.AsMemory(), FileWriteDisposition.CreateOrReplace, cancellationToken).ConfigureAwait(false);
        Require(result);
    }

    /// <inheritdoc/>
    protected override async ValueTask PersistAsync(SecurityAuthorizationContext authorization, ImmutableArray<ArtifactEntry> upserts, CancellationToken cancellationToken)
    {
        var record = Frame(new StoredArtifactLogRecord([.. upserts.Select(StoredArtifactEntry.FromDomain)]));
        var appended = await _effects.WriteAsync(authorization, _logName, record, FileWriteDisposition.Append, cancellationToken).ConfigureAwait(false);
        if (appended is FileWriteNotFound)
        {
            appended = await _effects.WriteAsync(authorization, _logName, record, FileWriteDisposition.CreateOnly, cancellationToken).ConfigureAwait(false);
            if (appended is FileWriteConflict)
            {
                appended = await _effects.WriteAsync(authorization, _logName, record, FileWriteDisposition.Append, cancellationToken).ConfigureAwait(false);
            }
        }

        Require(appended);
    }

    /// <inheritdoc/>
    protected override async ValueTask ReleasePayloadAsync(SecurityAuthorizationContext authorization, ArtifactEntry entry, bool stillReferenced, CancellationToken cancellationToken)
    {
        if (stillReferenced)
        {
            return;
        }

        RequireDeleted(await _effects.DeleteAsync(authorization, PayloadName(entry), cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc/>
    protected override ValueTask<byte[]?> OpenPayloadAsync(SecurityAuthorizationContext authorization, ArtifactEntry entry, CancellationToken cancellationToken) =>
        _effects.ReadAsync(authorization, PayloadName(entry), _settings.MaximumPayloadBytes, cancellationToken);

    private static string PayloadName(ArtifactEntry entry) => FileSystemArtifactLayout.PayloadName(entry.TenantId, entry.ContentHash);

    private static void Require(FileWriteResult result)
    {
        if (result is not FileWriteSuccess)
        {
            throw new FileEffectException("The file-system write was refused or did not commit.");
        }
    }

    private async ValueTask ReplayAsync(SecurityAuthorizationContext authorization, byte[] content, CancellationToken cancellationToken)
    {
        var records = 0;
        var start = 0;
        for (var index = 0; index < content.Length; index++)
        {
            if (content[index] != _newLine)
            {
                continue;
            }

            if (index > start)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var entry in JsonStoreSerialization.Decode<StoredArtifactLogRecord>(content.AsSpan(start, index - start), _json).Entries)
                {
                    State.Restore(entry.ToDomain());
                }

                records++;
            }

            start = index + 1;
        }

        var torn = start < content.Length;
        if (torn)
        {
            ArtifactStoreObservation.Safe(() => ArtifactStoreLog.RecoveredTornAppend(Logger, "file_system"));
        }

        if (torn || records > _settings.CompactionRecordThreshold)
        {
            await CompactAsync(authorization, records, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void RequireDeleted(FileDeleteResult result)
    {
        if (result is not (FileDeleteSuccess or FileDeleteNotFound))
        {
            throw new FileEffectException("The file-system delete was refused or did not commit.");
        }
    }

    private async ValueTask SweepUnreferencedPayloadsAsync(SecurityAuthorizationContext authorization, CancellationToken cancellationToken)
    {
        var live = State.Snapshot()
            .Where(static entry => entry.HoldsPayload)
            .Select(PayloadName)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var name in await _effects.EnumerateFilesAsync(authorization, cancellationToken).ConfigureAwait(false))
        {
            if (FileSystemArtifactLayout.IsPayloadName(name) && !live.Contains(name))
            {
                RequireDeleted(await _effects.DeleteAsync(authorization, name, cancellationToken).ConfigureAwait(false));
            }
        }
    }

    private byte[] Frame<TValue>(TValue value)
        where TValue : notnull
    {
        var encoded = JsonStoreSerialization.Encode(value, _json, _settings.MaximumRecordBytes);
        var framed = new byte[encoded.Length + 1];
        encoded.CopyTo(framed, 0);
        framed[^1] = _newLine;
        return framed;
    }

    private async ValueTask CompactAsync(SecurityAuthorizationContext authorization, int replayedRecords, CancellationToken cancellationToken)
    {
        var snapshot = State.Snapshot();
        using var buffer = new MemoryStream();
        foreach (var entry in snapshot)
        {
            buffer.Write(Frame(new StoredArtifactLogRecord([StoredArtifactEntry.FromDomain(entry)])));
        }

        var result = await _effects.WriteAsync(authorization, _logName, buffer.ToArray(), FileWriteDisposition.CreateOrReplace, cancellationToken).ConfigureAwait(false);
        Require(result);
        ArtifactStoreObservation.Safe(() => ArtifactStoreLog.Compacted(Logger, "file_system", replayedRecords, snapshot.Length));
    }
}
