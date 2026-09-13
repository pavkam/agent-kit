// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory;

/// <summary>Creates collision-resistant process-local lease identities for the in-memory adapter.</summary>
internal sealed class GuidExecutionLeaseIdGenerator: IIdentifierGenerator<ExecutionLeaseId>
{
    /// <inheritdoc/>
    public ExecutionLeaseId Create() => new(Guid.NewGuid());
}
