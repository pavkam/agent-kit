// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies spec FileWriteResult hierarchy contracts.</summary>
public sealed class SpecFileWriteResultTests
{
    [Fact]
    public void Hierarchy_EveryLeafDerivesFromFileWriteResult()
    {
        var target = new ResolvedFileTarget(
            new FileRootId("workspace"),
            new NormalizedRelativePath("notes.txt"),
            "/tmp/workspace/notes.txt",
            FilePathComparisonKind.Ordinal,
            new ContentHash("link"),
            new ContentHash("target"));

        FileWriteResult[] results =
        [
            new FileWriteSuccess(FileWriteOutcomeKind.Created, 1, 0, 1, new ContentHash("p"), new ContentHash("f")),
            new FileWriteNotFound(target),
            new FileWriteConflict(target, "conflict"),
            new FileWriteDenied("denied"),
            new FileWriteLimitExceeded(10, 11),
            new FileWriteCancelled(SideEffectCertainty.DefinitelyNotPerformed),
            new FileWriteUnsupported("unsupported"),
            new FileWriteFailed("failed"),
        ];

        results.Length.ShouldBe(8);
        foreach (var result in results)
        {
            _ = result.ShouldBeAssignableTo<FileWriteResult>();
        }
    }

    [Fact]
    public void FileWriteLimitExceeded_Constructor_WhenObservedBelowLimit_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new FileWriteLimitExceeded(10, 9));
        exception.ParamName.ShouldBe("observedBytes");
    }
}
