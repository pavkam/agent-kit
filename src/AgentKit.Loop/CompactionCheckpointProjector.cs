// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>
/// Projects the newest active <see cref="CompactionSessionEntry"/> of a branch into the in-memory
/// <see cref="RuntimeMessage"/> that stands in for the covered history in a run's model-facing view.
/// </summary>
/// <remarks>
/// <para>
/// The projector is pure: it reads only the supplied entry and cursor, allocates nothing durable, and never
/// touches a session, a provider, or a clock. The same entry always projects to the same message, including
/// the same <see cref="MessageId"/>, so two turns of one run, or two runs over one branch, present one
/// stable identity to context manifests and caches. The shape it produces is documented by
/// <see cref="CompactionCheckpointProjection"/>.
/// </para>
/// <para>
/// The message identity is derived, not generated: a SHA-256 digest over a fixed namespace and the
/// checkpoint entry's <see cref="SessionEntryId"/>, folded into a variant-1 UUID of version 8 (the
/// RFC 9562 custom-format version) so it can never collide with a random identity the injected
/// <see cref="IIdentifierGenerator{TIdentifier}"/> produces for a committed message, and never reuses the
/// entry identity's own GUID as a message identity. The projection is never committed, so this identity is
/// never appended to a session.
/// </para>
/// </remarks>
internal static class CompactionCheckpointProjector
{
    /// <summary>The fixed namespace mixed into every derived checkpoint message identity.</summary>
    private static readonly byte[] _messageIdNamespace = Encoding.UTF8.GetBytes("agentkit.loop.compaction-checkpoint.message-id.v1");

    /// <summary>Builds the model-facing projection of one active checkpoint.</summary>
    /// <param name="checkpoint">The active compaction entry to project; its record must carry a checkpoint.</param>
    /// <param name="cursor">The branch cursor whose agent, session, conversation, and branch coordinates the message adopts.</param>
    /// <returns>
    /// A complete <see cref="RuntimeMessage"/> whose parts are the <see cref="CompactionCheckpointProjection.HeaderText"/>
    /// followed by the checkpoint's summary parts, whose run and turn are those of the operation that activated
    /// the checkpoint when it ran inside a run, whose creation time is the entry's commit time, and whose
    /// extensions carry the provenance keys of <see cref="CompactionCheckpointProjection"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="checkpoint"/> or <paramref name="cursor"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="checkpoint"/> is not an active record carrying a checkpoint.</exception>
    public static RuntimeMessage Project(CompactionSessionEntry checkpoint, MessageCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(cursor);
        if (checkpoint.Record is not { Status: CompactionRecordStatus.Active, Checkpoint: { } content })
        {
            throw new ArgumentException("Only an active compaction record carrying a checkpoint can be projected.", nameof(checkpoint));
        }

        var manifest = checkpoint.Record.Manifest;
        var (runId, turnId) = checkpoint.Correlation is InRunOperationCorrelation inRun
            ? (inRun.RunId, inRun.TurnId)
            : ((RunId?) null, (TurnId?) null);

        ImmutableArray<ContentPart> parts =
        [
            new TextPart(CompactionCheckpointProjection.HeaderText, TextSemantics.Plain, ExtensionData.Empty),
            .. content.Summary,
        ];

        return new RuntimeMessage(
            DeriveMessageId(checkpoint.Id),
            cursor.AgentId,
            cursor.SessionId,
            cursor.ConversationId,
            cursor.BranchId,
            runId,
            turnId,
            checkpoint.RecordedAt,
            MessageState.Complete,
            parts,
            Provenance(checkpoint, manifest));
    }

    /// <summary>Derives the stable projection identity for one checkpoint entry.</summary>
    /// <param name="entryId">The identity of the projected <see cref="CompactionSessionEntry"/>.</param>
    /// <returns>A non-empty, version-8 variant-1 UUID that is the same for every call with the same entry identity.</returns>
    internal static MessageId DeriveMessageId(SessionEntryId entryId)
    {
        Span<byte> input = stackalloc byte[_messageIdNamespace.Length + 16];
        _messageIdNamespace.CopyTo(input);
        var written = entryId.Value.TryWriteBytes(input[_messageIdNamespace.Length..], bigEndian: true, out _);
        Debug.Assert(written, "A GUID always fits into sixteen bytes.");

        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        var hashed = SHA256.TryHashData(input, digest, out _);
        Debug.Assert(hashed, "The destination is exactly the SHA-256 digest size.");

        // Version 8 (custom) in the high nibble of octet 6, RFC 4122 variant in the top two bits of octet 8.
        digest[6] = (byte) ((digest[6] & 0x0F) | 0x80);
        digest[8] = (byte) ((digest[8] & 0x3F) | 0x80);
        return new MessageId(new Guid(digest[..16], bigEndian: true));
    }

    private static ExtensionData Provenance(CompactionSessionEntry checkpoint, CompactionManifest manifest)
    {
        Debug.Assert(checkpoint is not null && manifest is not null, "The caller validated the entry and read its manifest.");
        var values = ImmutableDictionary.CreateBuilder<string, ExtensionValue>(StringComparer.Ordinal);
        values.Add(CompactionCheckpointProjection.FormatKey, Json(CompactionCheckpointProjection.Format));
        values.Add(CompactionCheckpointProjection.CompactionIdKey, Json(checkpoint.Record.Context.CompactionId.ToString()));
        values.Add(CompactionCheckpointProjection.ManifestIdKey, Json(manifest.Id.ToString()));
        values.Add(CompactionCheckpointProjection.SessionEntryIdKey, Json(checkpoint.Id.ToString()));
        values.Add(CompactionCheckpointProjection.SequenceKey, Json(checkpoint.Sequence.Value));
        values.Add(CompactionCheckpointProjection.CoveredStartKey, Json(manifest.CoveredRange.StartInclusive.Value));
        values.Add(CompactionCheckpointProjection.CoveredEndKey, Json(manifest.CoveredRange.EndInclusive.Value));
        values.Add(CompactionCheckpointProjection.RetainedSuffixStartKey, Json(manifest.RetainedSuffixStart.Value));
        return new ExtensionData(values.ToImmutable());

        static ExtensionValue Json<T>(T value) => new([.. JsonSerializer.SerializeToUtf8Bytes(value)]);
    }
}
