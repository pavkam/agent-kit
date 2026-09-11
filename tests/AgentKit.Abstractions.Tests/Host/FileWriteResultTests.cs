// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies FileWriteResult behavior and contracts.</summary>
public sealed class FileWriteResultTests
{
    [Fact]
    public void FileWriteResult_Hierarchy_EveryLeafDerivesFromFileWriteResult()
    {
        FileWriteResult written = new FileWritten(1);
        FileWriteResult alreadyExists = new FileAlreadyExists(new FileSystemPath("a.txt"));
        FileWriteResult denied = new FileWriteDenied("no");
        FileWriteResult failed = new FileWriteFailed("no");
        _ = written.ShouldBeOfType<FileWritten>();
        _ = alreadyExists.ShouldBeOfType<FileAlreadyExists>();
        _ = denied.ShouldBeOfType<FileWriteDenied>();
        _ = failed.ShouldBeOfType<FileWriteFailed>();
    }
}
