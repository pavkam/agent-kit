// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Read;

/// <summary>Creates new <see cref="FileOperationId"/> values using random GUIDs.</summary>
internal sealed class GuidFileOperationIdGenerator: IIdentifierGenerator<FileOperationId>
{
    /// <inheritdoc/>
    public FileOperationId Create() => new(Guid.NewGuid());
}
