// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Encodes and decodes the bounded portable version-one <see cref="GoalCreatedSessionEntry"/> schema.</summary>
/// <remarks>The codec is immutable and thread-safe. A delegated child's captured authorization is never part of the schema; see <see cref="GoalCreatedSessionEntry"/>.</remarks>
public sealed class GoalCreatedSessionEntryCodec: ISessionEntryCodec
{
    /// <summary>Creates the immutable codec.</summary>
    public GoalCreatedSessionEntryCodec() => Descriptor = new(
        new SessionEntryTypeId("agentkit.goals/goal-created"),
        typeof(GoalCreatedSessionEntry),
        GoalSessionEntryCodecSupport.Version,
        [GoalSessionEntryCodecSupport.Version],
        GoalSessionEntryCodecSupport.Limits);

    /// <inheritdoc/>
    public SessionEntryCodecDescriptor Descriptor { get; }

    /// <inheritdoc/>
    public SessionEntryEncodeResult Encode(SessionEntry entry) =>
        GoalSessionEntryCodecSupport.Encode<GoalCreatedSessionEntry, GoalCreatedEntryDocument>(Descriptor, entry, GoalCreatedEntryDocument.From);

    /// <inheritdoc/>
    public SessionEntryDecodeResult Decode(SessionEntryWireEnvelope wire) =>
        GoalSessionEntryCodecSupport.Decode<GoalCreatedEntryDocument, GoalCreatedSessionEntry>(Descriptor, wire, static document => document.ToEntry());
}
