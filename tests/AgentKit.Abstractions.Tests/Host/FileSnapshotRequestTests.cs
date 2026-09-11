// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;



/// <summary>Verifies FileSnapshotRequest behavior and contracts.</summary>
public sealed class FileSnapshotRequestTests
{
    [Fact]
    public void FileSnapshotRequest_WhenMaximumBytesIsNotPositive_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new FileSnapshotRequest(new FileSystemPath("a.txt"), 0, SecurityTestData.Grant()));
        exception.ParamName.ShouldBe("maximumBytes");
    }
}
