// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Json;

using System.Security.Cryptography;
using System.Text;

/// <summary>Persists artifact entries as a flushed newline-delimited log and payloads as tenant-partitioned content-addressed files.</summary>
/// <remarks>
/// <para>
/// A plan is committed in a fixed order: the payload file is written atomically, then one log record holding every entry of the plan
/// is appended and flushed, then released payload files are deleted. A crash between steps can leave an unreferenced file, which
/// recovery sweeps away, but can never leave a committed entry without bytes or readable bytes without an entry.
/// </para>
/// <para>
/// Payload files live under a directory named by the SHA-256 of the tenant, so identical content in two tenants is two files and
/// neither tenant's existence is observable through the other. Within a tenant identical content is stored once and released only
/// when no live entry references it.
/// </para>
/// </remarks>
internal sealed class JsonArtifactBackend: ArtifactStateBackend
{
    private readonly JsonArtifactFile _file;

    internal JsonArtifactBackend(JsonArtifactFile file, ILogger? logger)
        : base("json", logger)
    {
        ArgumentNullException.ThrowIfNull(file);
        _file = file;
    }

    /// <inheritdoc/>
    protected override ValueTask RecoverAsync(SecurityAuthorizationContext? authorization, CancellationToken cancellationToken)
    {
        var records = _file.Open(VerifyEncoding, cancellationToken);
        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var entry in _file.Decode<StoredArtifactLogRecord>(record.Span).Entries)
            {
                State.Restore(entry.ToDomain());
            }
        }

        if (_file.NeedsCompaction)
        {
            var snapshot = State.Snapshot();
            _file.Compact(
                [.. snapshot.Select(entry => _file.Encode(new StoredArtifactLogRecord([StoredArtifactEntry.FromDomain(entry)])))],
                snapshot.Length,
                cancellationToken);
        }

        SweepUnreferencedPayloads();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    protected override ValueTask StagePayloadAsync(SecurityAuthorizationContext authorization, ArtifactEntry entry, ImmutableArray<byte> content, CancellationToken cancellationToken)
    {
        var path = PayloadPath(entry.TenantId, entry.ContentHash);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        JsonAtomicDocument.Replace(path, [.. content], cancellationToken);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    protected override ValueTask PersistAsync(SecurityAuthorizationContext authorization, ImmutableArray<ArtifactEntry> upserts, CancellationToken cancellationToken)
    {
        _file.Append(_file.Encode(new StoredArtifactLogRecord([.. upserts.Select(StoredArtifactEntry.FromDomain)])), cancellationToken);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    protected override ValueTask ReleasePayloadAsync(SecurityAuthorizationContext authorization, ArtifactEntry entry, bool stillReferenced, CancellationToken cancellationToken)
    {
        if (!stillReferenced)
        {
            File.Delete(PayloadPath(entry.TenantId, entry.ContentHash));
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    protected override ValueTask<byte[]?> OpenPayloadAsync(SecurityAuthorizationContext authorization, ArtifactEntry entry, CancellationToken cancellationToken) =>
        ValueTask.FromResult(JsonAtomicDocument.Read(PayloadPath(entry.TenantId, entry.ContentHash), _file.MaximumPayloadBytes));

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _file.Dispose();
        }

        base.Dispose(disposing);
    }

    private static string TenantDirectory(TenantId tenant) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(tenant.Value)));

    private static string HashFileName(ContentHash hash)
    {
        var value = hash.Value;
        var separator = value.IndexOf(':', StringComparison.Ordinal);
        var digest = separator >= 0 ? value[(separator + 1)..] : value;
        return digest.Length > 0 && digest.All(static character => char.IsAsciiHexDigitLower(character))
            ? digest + ".bin"
            : throw new InvalidDataException("A content hash cannot name a payload file.");
    }

    private string PayloadPath(TenantId tenant, ContentHash hash) => Path.Combine(_file.PayloadRoot, TenantDirectory(tenant), HashFileName(hash));

    private void VerifyEncoding()
    {
        var probeMetadata = new StoredArtifactMetadata(
            "probe", "text/probe", 1, "sha256:aa", DataClassification.Internal, ArtifactOwnershipKind.External, ArtifactMutability.ExternallyManaged,
            "probe", DateTimeOffset.UnixEpoch, true, "probe:1", "https://probe.example.com/1", true);
        var probe = new StoredArtifactEntry(
            "probe", Guid.Parse("a0000000-0000-0000-0000-000000000001"), Guid.Parse("a0000000-0000-0000-0000-000000000002"), "1", "probe", 1,
            "probe", "probe", probeMetadata, "sha256:aa", "probe", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddSeconds(1),
            ArtifactEntryState.Finalized, new StoredArtifactReference("probe", 1, "sha256:aa", DateTimeOffset.UnixEpoch, 1, DateTimeOffset.UnixEpoch),
            DateTimeOffset.UnixEpoch);
        _file.Verify(new StoredArtifactLogRecord([probe]));
    }

    private void SweepUnreferencedPayloads()
    {
        var root = _file.PayloadRoot;
        if (!Directory.Exists(root))
        {
            return;
        }

        var live = State.Snapshot()
            .Where(static entry => entry.HoldsPayload)
            .Select(static entry => (Directory: TenantDirectory(entry.TenantId), File: HashFileName(entry.ContentHash)))
            .ToHashSet();
        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            var name = Path.GetFileName(directory);
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                if (!live.Contains((name, Path.GetFileName(file))))
                {
                    JsonAtomicDocument.TryDelete(file);
                }
            }
        }
    }
}
