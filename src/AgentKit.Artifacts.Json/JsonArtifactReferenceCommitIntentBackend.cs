// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Json;

/// <summary>Persists reference-commit intents as a flushed newline-delimited log with one complete intent per record.</summary>
/// <remarks>
/// Every change appends one flushed record holding the whole intent, so an acknowledged intent or transition survives process loss and
/// the last record of a tenant preparation wins on replay. A torn trailing append is recovered or refused according to the target, and
/// a log that grew past the compaction threshold is rewritten as one record per intent when it is opened.
/// </remarks>
internal sealed class JsonArtifactReferenceCommitIntentBackend: ArtifactReferenceCommitIntentStateBackend
{
    private readonly JsonArtifactFile _file;

    internal JsonArtifactReferenceCommitIntentBackend(JsonArtifactFile file, TimeProvider time, ILogger? logger)
        : base("json", time, logger)
    {
        ArgumentNullException.ThrowIfNull(file);
        _file = file;
    }

    /// <inheritdoc/>
    protected override void Recover(ArtifactReferenceCommitIntentTable table, CancellationToken cancellationToken)
    {
        var records = _file.Open(VerifyEncoding, cancellationToken);
        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            table.Restore(_file.Decode<StoredArtifactReferenceCommitIntent>(record.Span).ToDomain());
        }

        if (_file.NeedsCompaction)
        {
            var snapshot = table.Snapshot();
            _file.Compact(
                [.. snapshot.Select(intent => _file.Encode(StoredArtifactReferenceCommitIntent.FromDomain(intent)))],
                snapshot.Length,
                cancellationToken);
        }
    }

    /// <inheritdoc/>
    protected override void Persist(ArtifactReferenceCommitIntent intent, CancellationToken cancellationToken) =>
        _file.Append(_file.Encode(StoredArtifactReferenceCommitIntent.FromDomain(intent)), cancellationToken);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _file.Dispose();
        }

        base.Dispose(disposing);
    }

    private void VerifyEncoding() =>
        _file.Verify(new StoredArtifactReferenceCommitIntent(
            Guid.Parse("a0000000-0000-0000-0000-000000000001"), "probe", Guid.Parse("a0000000-0000-0000-0000-000000000002"),
            Guid.Parse("a0000000-0000-0000-0000-000000000003"), "1", "probe", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddSeconds(1),
            ArtifactReferenceCommitState.Pending, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch));
}
