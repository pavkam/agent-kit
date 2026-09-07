// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Edit;

/// <summary>Creates collision-resistant workspace mutation identities for private staging correlation.</summary>
internal sealed class GuidWorkspaceMutationIdGenerator: IIdentifierGenerator<WorkspaceMutationId>
{
    /// <inheritdoc/>
    public WorkspaceMutationId Create() => new(Guid.NewGuid());
}
