// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Encodes and decodes the bounded portable version-one <see cref="GoalTransitionSessionEntry"/> schema.</summary>
/// <remarks>The codec is immutable and thread-safe.</remarks>
public sealed class GoalTransitionSessionEntryCodec: ISessionEntryCodec
{
    /// <summary>Creates the immutable codec.</summary>
    public GoalTransitionSessionEntryCodec() => Descriptor = new(
        new SessionEntryTypeId("agentkit.goals/goal-transition"),
        typeof(GoalTransitionSessionEntry),
        GoalSessionEntryCodecSupport.Version,
        [GoalSessionEntryCodecSupport.Version],
        GoalSessionEntryCodecSupport.Limits);

    /// <inheritdoc/>
    public SessionEntryCodecDescriptor Descriptor { get; }

    /// <inheritdoc/>
    public SessionEntryEncodeResult Encode(SessionEntry entry) =>
        GoalSessionEntryCodecSupport.Encode<GoalTransitionSessionEntry, GoalTransitionEntryDocument>(Descriptor, entry, GoalTransitionEntryDocument.From);

    /// <inheritdoc/>
    public SessionEntryDecodeResult Decode(SessionEntryWireEnvelope wire) =>
        GoalSessionEntryCodecSupport.Decode<GoalTransitionEntryDocument, GoalTransitionSessionEntry>(Descriptor, wire, static document => document.ToEntry());
}
