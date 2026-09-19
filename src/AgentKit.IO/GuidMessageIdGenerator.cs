// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Creates distinct message identities for newly materialized promotion history.</summary>
internal sealed class GuidMessageIdGenerator: IIdentifierGenerator<MessageId>
{
    /// <summary>Creates a non-default identity distinct from every other process-local allocation.</summary>
    /// <returns>A new non-default message identity.</returns>
    public MessageId Create() => new(Guid.NewGuid());
}
