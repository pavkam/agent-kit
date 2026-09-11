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
}
