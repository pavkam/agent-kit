// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Creates random operation identities for judge requests when the composition registers no generator.</summary>
internal sealed class GuidOperationIdGenerator: IIdentifierGenerator<OperationId>
{
    /// <inheritdoc/>
    public OperationId Create() => new(Guid.NewGuid());
}
