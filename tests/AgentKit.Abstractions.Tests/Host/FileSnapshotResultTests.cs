// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;



/// <summary>Verifies FileSnapshotResult behavior and contracts.</summary>
public sealed class FileSnapshotResultTests
{
    [Fact]
    public void FileSnapshotResult_WhenEquivalentByteArraysDifferByInstance_IsStructurallyEqual()
    {
        var fingerprint = new ContentHash("sha256:test");
        var left = new FileSnapshotResult(FileSnapshotStatus.Success, [1, 2, 3], fingerprint, null);
        var right = new FileSnapshotResult(FileSnapshotStatus.Success, [1, 2, 3], fingerprint, null);
        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }
}
