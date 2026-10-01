// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.FileSystem;

/// <summary>Creates unique file-operation identities for default file-system artifact stores.</summary>
internal sealed class GuidFileOperationIdGenerator: IIdentifierGenerator<FileOperationId>
{
    /// <summary>Creates a fresh identity that correlates exactly one file effect.</summary>
    /// <returns>A non-default identity.</returns>
    public FileOperationId Create() => new(Guid.NewGuid());
}
