// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Plan;

/// <summary>Generates unpredictable session-entry identities for plan revisions.</summary>
internal sealed class GuidSessionEntryIdGenerator: IIdentifierGenerator<SessionEntryId>
{
    /// <inheritdoc/>
    public SessionEntryId Create() => new(Guid.NewGuid());
}
