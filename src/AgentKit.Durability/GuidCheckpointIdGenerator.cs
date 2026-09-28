// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

/// <summary>Creates checkpoint identities from cryptographically random GUIDs.</summary>
/// <remarks>
/// This is the replaceable default. Tests and deterministic replay register their own
/// <see cref="IIdentifierGenerator{T}"/> so checkpoint identity is reproducible; durable replay never depends on the
/// values this generator happened to produce.
/// </remarks>
internal sealed class GuidCheckpointIdGenerator: IIdentifierGenerator<CheckpointId>
{
    /// <inheritdoc/>
    public CheckpointId Create() => new(Guid.NewGuid());
}
