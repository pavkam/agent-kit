// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Creates distinct session-entry identities for newly materialized promotion history.</summary>
internal sealed class GuidSessionEntryIdGenerator: IIdentifierGenerator<SessionEntryId>
{
    /// <summary>Creates a non-default identity distinct from every other process-local allocation.</summary>
    /// <returns>A new non-default session-entry identity.</returns>
    public SessionEntryId Create() => new(Guid.NewGuid());
}
