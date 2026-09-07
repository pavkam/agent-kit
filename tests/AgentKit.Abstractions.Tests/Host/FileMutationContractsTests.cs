// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

public sealed class FileMutationContractsTests
{
    [Fact]
    public void FileSnapshotRequest_WhenMaximumBytesIsNotPositive_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new FileSnapshotRequest(
            new FileSystemPath("a.txt"), 0, SecurityTestData.Grant()));

        exception.ParamName.ShouldBe("maximumBytes");
    }

    [Fact]
    public void FileSnapshotResult_WhenEquivalentByteArraysDifferByInstance_IsStructurallyEqual()
    {
        var fingerprint = new ContentHash("sha256:test");
        var left = new FileSnapshotResult(FileSnapshotStatus.Success, [1, 2, 3], fingerprint, null);
        var right = new FileSnapshotResult(FileSnapshotStatus.Success, [1, 2, 3], fingerprint, null);

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void AtomicReplaceStagingPath_WhenTargetNested_StaysInSameDirectory()
    {
        var id = new WorkspaceMutationId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var path = new FileSystemPath("src/a.cs");

        var staging = FileSecurityBinding.AtomicReplaceStagingPath(id, path);

        staging.Value.ShouldBe("src/.agentkit-stage-10000000000000000000000000000001.tmp");
        FileSecurityBinding.AtomicReplaceResources(id, path).ShouldBe([
            FileSecurityBinding.Resource(path),
            FileSecurityBinding.Resource(staging),
        ]);
    }

    [Fact]
    public void AtomicReplaceFingerprint_WhenMutationEvidenceChanges_ChangesEvidence()
    {
        var firstId = new WorkspaceMutationId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var secondId = new WorkspaceMutationId(Guid.Parse("20000000-0000-0000-0000-000000000002"));
        var path = new FileSystemPath("a.txt");
        var expected = new ContentHash("sha256:old");
        var baseline = FileSecurityBinding.AtomicReplaceFingerprint(firstId, path, expected, [1, 2]);
        var variants = new[]
        {
            FileSecurityBinding.AtomicReplaceFingerprint(secondId, path, expected, [1, 2]),
            FileSecurityBinding.AtomicReplaceFingerprint(firstId, new FileSystemPath("b.txt"), expected, [1, 2]),
            FileSecurityBinding.AtomicReplaceFingerprint(firstId, path, new ContentHash("sha256:other"), [1, 2]),
            FileSecurityBinding.AtomicReplaceFingerprint(firstId, path, expected, [1, 3]),
        };

        variants.ShouldAllBe(value => value != baseline);
        variants.Distinct().Count().ShouldBe(variants.Length);
    }

    [Fact]
    public void AtomicFileReplaceRequest_WhenContentIsDefault_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new AtomicFileReplaceRequest(
            new WorkspaceMutationId(Guid.NewGuid()),
            new FileSystemPath("a.txt"),
            new ContentHash("sha256:old"),
            default,
            SecurityTestData.Grant()));

        exception.ParamName.ShouldBe("content");
    }

    [Fact]
    public void WorkspacePatchRequest_WhenEntriesContainNull_ThrowsExactParameter()
    {
        ImmutableArray<WorkspacePatchEntry> entries = [null!];

        var exception = Should.Throw<ArgumentException>(() => new WorkspacePatchRequest(entries));

        exception.ParamName.ShouldBe("entries");
    }

    [Fact]
    public void WorkspacePatchMove_WhenDestinationEqualsSource_ThrowsExactParameter()
    {
        var path = new FileSystemPath("a.txt");

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new WorkspacePatchMove(
            new WorkspaceMutationId(Guid.NewGuid()),
            path,
            path,
            new ContentHash("sha256:old"),
            SecurityTestData.Grant()));

        exception.ParamName.ShouldBe("destinationPath");
    }

    [Fact]
    public void WorkspacePatchCreate_WhenContentIsDefault_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new WorkspacePatchCreate(
            new WorkspaceMutationId(Guid.NewGuid()),
            new FileSystemPath("a.txt"),
            default,
            SecurityTestData.Grant()));

        exception.ParamName.ShouldBe("content");
    }

    [Fact]
    public void WorkspacePatchSecurityBinding_WhenCreateEvidenceChanges_ChangesFingerprint()
    {
        var firstId = new WorkspaceMutationId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var secondId = new WorkspaceMutationId(Guid.Parse("20000000-0000-0000-0000-000000000002"));
        var path = new FileSystemPath("a.txt");
        var baseline = WorkspacePatchSecurityBinding.CreateFingerprint(firstId, path, [1, 2]);
        var variants = new[]
        {
            WorkspacePatchSecurityBinding.CreateFingerprint(secondId, path, [1, 2]),
            WorkspacePatchSecurityBinding.CreateFingerprint(firstId, new FileSystemPath("b.txt"), [1, 2]),
            WorkspacePatchSecurityBinding.CreateFingerprint(firstId, path, [1, 3]),
        };

        variants.ShouldAllBe(value => value != baseline);
        WorkspacePatchSecurityBinding.CreateResources(firstId, path).ShouldBe(
            FileSecurityBinding.AtomicReplaceResources(firstId, path));
    }

    [Fact]
    public void WorkspacePatchResult_WhenEquivalentEntryArraysDifferByInstance_IsStructurallyEqual()
    {
        var path = new FileSystemPath("a.txt");
        var left = new WorkspacePatchResult(
            WorkspacePatchStatus.AtomicCommitted,
            [new WorkspacePatchEntryResult(
                0,
                WorkspacePatchEntryKind.Create,
                WorkspacePatchEntryStatus.Committed,
                path,
                null,
                new ContentHash("sha256:new"),
                null)],
            null);
        var right = new WorkspacePatchResult(
            WorkspacePatchStatus.AtomicCommitted,
            [new WorkspacePatchEntryResult(
                0,
                WorkspacePatchEntryKind.Create,
                WorkspacePatchEntryStatus.Committed,
                path,
                null,
                new ContentHash("sha256:new"),
                null)],
            null);

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }
}
