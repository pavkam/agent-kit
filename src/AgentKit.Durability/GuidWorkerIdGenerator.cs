// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

/// <summary>Creates worker identities from cryptographically random GUIDs.</summary>
/// <remarks>
/// A fresh identity per attempt is what makes takeover observable: a recovered attempt presents a different worker
/// identity than the owner it fenced out. This is the replaceable default; tests register a deterministic generator.
/// </remarks>
internal sealed class GuidWorkerIdGenerator: IIdentifierGenerator<WorkerId>
{
    /// <inheritdoc/>
    public WorkerId Create() => new(Guid.NewGuid());
}
