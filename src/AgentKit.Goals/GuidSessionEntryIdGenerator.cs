// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Creates unpredictable session-entry identities for the session-backed goal store registration.</summary>
internal sealed class GuidSessionEntryIdGenerator: IIdentifierGenerator<SessionEntryId>
{
    /// <summary>Creates a new non-default session-entry identity.</summary>
    /// <returns>A fresh identity.</returns>
    public SessionEntryId Create() => new(Guid.NewGuid());
}
