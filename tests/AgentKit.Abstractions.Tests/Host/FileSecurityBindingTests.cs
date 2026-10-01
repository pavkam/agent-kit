// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;



/// <summary>Verifies FileSecurityBinding behavior and contracts.</summary>
public sealed class FileSecurityBindingTests
{
    [Fact]
    public void AtomicReplaceStagingPath_WhenTargetNested_StaysInSameDirectory()
    {
        var id = new WorkspaceMutationId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var path = new FileSystemPath("src/a.cs");
        var staging = FileSecurityBinding.AtomicReplaceStagingPath(id, path);
        staging.Value.ShouldBe("src/.agentkit-stage-10000000000000000000000000000001.tmp");
        FileSecurityBinding.AtomicReplaceResources(id, path).ShouldBe([FileSecurityBinding.Resource(path), FileSecurityBinding.Resource(staging),]);
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
    public void DeleteFingerprint_WhenTargetOrPreconditionChanges_ChangesEvidence()
    {
        var root = new FileRootId("workspace");
        var target = new FileTarget(root, new NormalizedRelativePath("a.txt"));
        var baseline = FileSecurityBinding.DeleteFingerprint(target, null);
        var variants = new[]
        {
            FileSecurityBinding.DeleteFingerprint(new FileTarget(root, new NormalizedRelativePath("b.txt")), null),
            FileSecurityBinding.DeleteFingerprint(new FileTarget(new FileRootId("other"), new NormalizedRelativePath("a.txt")), null),
            FileSecurityBinding.DeleteFingerprint(target, new ContentHash("sha256:current")),
        };
        variants.ShouldAllBe(value => value != baseline);
        variants.Distinct().Count().ShouldBe(variants.Length);
    }

    [Fact]
    public void DeleteFingerprint_WhenOperationAndLogicalTargetAgree_ProduceTheSameEvidence()
    {
        var path = new NormalizedRelativePath("a.txt");
        var resolved = new ResolvedFileTarget(
            new FileRootId("workspace"), path, "/host/a.txt", FilePathComparisonKind.Ordinal, new ContentHash("link"), new ContentHash("target"));
        var operation = new AuthorizedFileDelete(resolved, new ContentHash("sha256:current"), SecurityTestData.Grant());

        FileSecurityBinding.DeleteFingerprint(operation).ShouldBe(
            FileSecurityBinding.DeleteFingerprint(new FileTarget(new FileRootId("workspace"), path), new ContentHash("sha256:current")));
    }

    [Fact]
    public void DeleteFingerprint_WhenArgumentIsNull_ThrowsNamingIt()
    {
        Should.Throw<ArgumentNullException>(() => FileSecurityBinding.DeleteFingerprint(null!)).ParamName.ShouldBe("operation");
        Should.Throw<ArgumentNullException>(() => FileSecurityBinding.DeleteFingerprint(null!, null)).ParamName.ShouldBe("target");
    }

    [Fact]
    public void DeleteFingerprint_WhenComparedWithWriteEvidence_NeverCollides()
    {
        var target = new FileTarget(new FileRootId("workspace"), new NormalizedRelativePath("a.txt"));
        var hash = new ContentHash("sha256:x");

        FileSecurityBinding.DeleteFingerprint(target, null).ShouldNotBe(FileSecurityBinding.WriteFingerprint(
            target, FileWriteDisposition.ReplaceExisting, null, 0, hash, FileWriteAtomicityMode.Required, FileWriteEffectClass.WorkspaceBytes, hash));
    }

    [Fact]
    public void ReadFingerprint_WhenPathChanges_ChangesEvidence()
    {
        var first = FileSecurityBinding.ReadFingerprint(new FileSystemPath("a.txt"));
        var second = FileSecurityBinding.ReadFingerprint(new FileSystemPath("b.txt"));
        second.ShouldNotBe(first);
    }

    [Fact]
    public void SnapshotFingerprint_WhenMaximumBytesIsNotPositive_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => FileSecurityBinding.SnapshotFingerprint(new FileSystemPath("a.txt"), 0)).ParamName.ShouldBe("maximumBytes");

    [Fact]
    public void SnapshotFingerprint_WhenMaximumBytesChanges_ChangesEvidence()
    {
        var path = new FileSystemPath("a.txt");
        var first = FileSecurityBinding.SnapshotFingerprint(path, 100);
        var second = FileSecurityBinding.SnapshotFingerprint(path, 200);
        second.ShouldNotBe(first);
    }
}
