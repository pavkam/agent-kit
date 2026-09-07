// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes;

/// <summary>Creates collision-resistant process-operation identities for ordinary live execution.</summary>
/// <remarks>Tests and replay compositions replace this default with a deterministic generator.</remarks>
internal sealed class GuidProcessOperationIdGenerator: IIdentifierGenerator<ProcessOperationId>
{
    /// <inheritdoc/>
    public ProcessOperationId Create() => new(Guid.NewGuid());
}
