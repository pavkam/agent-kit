// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies FileReadResult behavior and contracts.</summary>
public sealed class FileReadResultTests
{
    [Fact]
    public void FileReadResult_Hierarchy_EveryLeafDerivesFromFileReadResult()
    {
        FileReadResult read = new FileRead("x", 1);
        FileReadResult notFound = new FileNotFound(new FileSystemPath("a.txt"));
        FileReadResult denied = new FileReadDenied("no");
        FileReadResult failed = new FileReadFailed("no");
        _ = read.ShouldBeOfType<FileRead>();
        _ = notFound.ShouldBeOfType<FileNotFound>();
        _ = denied.ShouldBeOfType<FileReadDenied>();
        _ = failed.ShouldBeOfType<FileReadFailed>();
    }
}
