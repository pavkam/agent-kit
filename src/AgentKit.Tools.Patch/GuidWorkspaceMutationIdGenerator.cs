// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Patch;

/// <summary>Creates collision-resistant mutation identities for independently authorized patch entries.</summary>
internal sealed class GuidWorkspaceMutationIdGenerator: IIdentifierGenerator<WorkspaceMutationId>
{
    /// <inheritdoc/>
    public WorkspaceMutationId Create() => new(Guid.NewGuid());
}
