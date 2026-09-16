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

    [Fact]
    public void WriteFingerprint_WhenContentIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => FileSecurityBinding.WriteFingerprint(new FileSystemPath("a.txt"), null!, FileWriteMode.CreateNew)).ParamName.ShouldBe("content");

    [Fact]
    public void WriteFingerprint_WhenModeIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => FileSecurityBinding.WriteFingerprint(new FileSystemPath("a.txt"), "text", (FileWriteMode) 99)).ParamName.ShouldBe("mode");

    [Fact]
    public void WriteFingerprint_WhenModeChanges_ChangesEvidence()
    {
        var path = new FileSystemPath("a.txt");
        var first = FileSecurityBinding.WriteFingerprint(path, "text", FileWriteMode.CreateNew);
        var second = FileSecurityBinding.WriteFingerprint(path, "text", FileWriteMode.Append);
        second.ShouldNotBe(first);
    }

    [Theory]
    [InlineData(FileWriteMode.CreateOrOverwrite, SecurityEffect.CreateOrReplace)]
    [InlineData(FileWriteMode.CreateNew, SecurityEffect.Create)]
    [InlineData(FileWriteMode.ReplaceExisting, SecurityEffect.Replace)]
    [InlineData(FileWriteMode.Append, SecurityEffect.Append)]
    public void WriteEffect_WhenModeIsDefined_ReturnsExpectedEffect(FileWriteMode mode, SecurityEffect expected) =>
        FileSecurityBinding.WriteEffect(mode).ShouldBe(expected);

    [Fact]
    public void WriteEffect_WhenModeIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => FileSecurityBinding.WriteEffect((FileWriteMode) 99)).ParamName.ShouldBe("mode");
}
