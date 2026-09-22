// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.InMemory.Tests;

using AgentKit.Conformance;

/// <summary>Runs shared file-system conformance against the in-memory adapter.</summary>
public sealed class InMemoryFileSystemConformanceTests: FileSystemConformanceTests<InMemoryFileSystemConformanceFixture>
{
    /// <inheritdoc/>
    [Obsolete]
    protected override InMemoryFileSystemConformanceFixture CreateFixture() => new();
}
