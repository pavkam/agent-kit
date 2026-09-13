// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Creates distinct admission identities for newly coordinated input.</summary>
/// <remarks>Equivalent replay reuses the durable identity already recorded by the queue; this generator is consulted for each attempt and never reconstructs a prior identity.</remarks>
internal sealed class GuidAdmissionIdGenerator: IIdentifierGenerator<AdmissionId>
{
    /// <summary>Creates a non-default identity distinct from every other process-local allocation.</summary>
    /// <returns>A new non-default admission identity.</returns>
    public AdmissionId Create() => new(Guid.NewGuid());
}
