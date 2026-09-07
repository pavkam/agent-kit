// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Task;

/// <summary>Generates unpredictable delegation identities for the default task tool registration.</summary>
internal sealed class GuidDelegationIdGenerator: IIdentifierGenerator<DelegationId>
{
    /// <inheritdoc/>
    public DelegationId Create() => new(Guid.NewGuid());
}
