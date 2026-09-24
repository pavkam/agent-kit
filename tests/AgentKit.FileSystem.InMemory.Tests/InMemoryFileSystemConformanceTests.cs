// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.InMemory.Tests;

using AgentKit.Conformance;

/// <summary>Runs shared file-system conformance against the in-memory adapter.</summary>
public sealed class InMemoryFileSystemConformanceTests: FileSystemConformanceTests<InMemoryFileSystemConformanceFixture>
{
    /// <inheritdoc/>
#pragma warning disable CS0618 // Fixture constructor exercises legacy host wiring under test.
    protected override InMemoryFileSystemConformanceFixture CreateFixture() => new();
#pragma warning restore CS0618
}
