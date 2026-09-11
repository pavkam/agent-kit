// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies DirectoryEnumerationResult behavior and contracts.</summary>
public sealed class DirectoryEnumerationResultTests
{
    [Fact]
    public void DirectoryEnumerationResult_WhenEntriesAreDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new DirectoryEnumerationResult(DirectoryEnumerationStatus.Success, default, new ContentHash("sha256:test"), null, null));
        exception.ParamName.ShouldBe("entries");
    }

    [Fact]
    public void DirectoryEnumerationResult_WhenEquivalentArraysDifferByInstance_RemainsStructurallyEqual()
    {
        var first = new DirectoryEnumerationResult(DirectoryEnumerationStatus.Success, [new DirectoryEntry(new FileSystemPath("a.txt"))], new ContentHash("sha256:test"), null, null);
        var second = new DirectoryEnumerationResult(DirectoryEnumerationStatus.Success, [new DirectoryEntry(new FileSystemPath("a.txt"))], new ContentHash("sha256:test"), null, null);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }
}
