// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Project;

/// <summary>Creates random <see cref="FileOperationId"/> values for project instruction reads.</summary>
internal sealed class GuidFileOperationIdGenerator: IIdentifierGenerator<FileOperationId>
{
    /// <inheritdoc/>
    public FileOperationId Create() => new(Guid.NewGuid());
}
