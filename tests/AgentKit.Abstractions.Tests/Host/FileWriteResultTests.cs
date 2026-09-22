// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies LegacyFileWriteResult behavior and contracts.</summary>
public sealed class LegacyFileWriteResultTests
{
    [Fact]
    public void FileWriteResult_Hierarchy_EveryLeafDerivesFromFileWriteResult()
    {
        LegacyFileWriteResult written = new LegacyFileWritten(1);
        LegacyFileWriteResult alreadyExists = new LegacyFileAlreadyExists(new FileSystemPath("a.txt"));
        LegacyFileWriteResult denied = new LegacyFileWriteDenied("no");
        LegacyFileWriteResult failed = new LegacyFileWriteFailed("no");
        _ = written.ShouldBeOfType<LegacyFileWritten>();
        _ = alreadyExists.ShouldBeOfType<LegacyFileAlreadyExists>();
        _ = denied.ShouldBeOfType<LegacyFileWriteDenied>();
        _ = failed.ShouldBeOfType<LegacyFileWriteFailed>();
    }
}
