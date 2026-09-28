// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

/// <summary>The journaled manifest of one turn's model attempt.</summary>
/// <param name="TurnId">The turn whose single attempt this operation represents.</param>
/// <param name="ModelRequestId">The model-request identity the attempt was prepared under.</param>
/// <param name="ModelAlias">The alias of the model the selection resolved to.</param>
/// <param name="MessageCount">The number of messages the assembled request carried.</param>
/// <remarks>
/// This is a manifest, not a serialized request: it identifies the attempt and records enough to detect that a replay
/// is describing different work, without persisting prompt content. The alias is configuration, not content.
/// </remarks>
internal sealed record DurableModelRequestState(
    Guid TurnId,
    Guid ModelRequestId,
    string ModelAlias,
    int MessageCount);
