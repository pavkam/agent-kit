// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.Tests;

using AgentKit.Conformance;

/// <summary>Runs shared file-system conformance against the operating-system adapter.</summary>
public sealed class OperatingSystemFileSystemConformanceTests: FileSystemConformanceTests<OperatingSystemFileSystemConformanceFixture>
{
    /// <inheritdoc/>
    protected override OperatingSystemFileSystemConformanceFixture CreateFixture() => new();
}
