// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

public sealed class DirectoryEnumerationContractsTests
{
    [Fact]
    public void DirectoryEnumerationCursor_WhenNextIndexIsZero_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new DirectoryEnumerationCursor(new ContentHash("sha256:test"), 0));

        exception.ParamName.ShouldBe("nextIndex");
    }

    [Fact]
    public void DirectoryEnumerationRequest_WhenMaximumEntriesIsZero_ThrowsBeforeGrantAssignment()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new DirectoryEnumerationRequest(null, 0, null, SecurityTestData.Grant()));

        exception.ParamName.ShouldBe("maximumEntries");
    }

    [Fact]
    public void DirectoryEnumerationRequest_WhenGrantIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new DirectoryEnumerationRequest(null, 1, null, null!));

        exception.ParamName.ShouldBe("grant");
    }

    [Fact]
    public void DirectoryEnumerationResult_WhenEntriesAreDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new DirectoryEnumerationResult(
            DirectoryEnumerationStatus.Success,
            default,
            new ContentHash("sha256:test"),
            null,
            null));

        exception.ParamName.ShouldBe("entries");
    }

    [Fact]
    public void DirectoryEnumerationResult_WhenEquivalentArraysDifferByInstance_RemainsStructurallyEqual()
    {
        var first = new DirectoryEnumerationResult(
            DirectoryEnumerationStatus.Success,
            [new DirectoryEntry(new FileSystemPath("a.txt"))],
            new ContentHash("sha256:test"),
            null,
            null);
        var second = new DirectoryEnumerationResult(
            DirectoryEnumerationStatus.Success,
            [new DirectoryEntry(new FileSystemPath("a.txt"))],
            new ContentHash("sha256:test"),
            null,
            null);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void DirectorySecurityBinding_WhenContinuationChanges_ChangesFingerprint()
    {
        var path = new FileSystemPath("src");

        var first = DirectorySecurityBinding.Fingerprint(path, 10, null);
        var resumed = DirectorySecurityBinding.Fingerprint(
            path,
            10,
            new DirectoryEnumerationCursor(new ContentHash("sha256:snapshot"), 2));

        resumed.ShouldNotBe(first);
    }
}
