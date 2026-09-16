// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;



/// <summary>Verifies WorkspacePatchSecurityBinding behavior and contracts.</summary>
public sealed class WorkspacePatchSecurityBindingTests
{
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
        WorkspacePatchSecurityBinding.CreateResources(firstId, path).ShouldBe(FileSecurityBinding.AtomicReplaceResources(firstId, path));
    }

    [Fact]
    public void DeleteResources_WhenCalled_ReturnsTargetResource()
    {
        var path = new FileSystemPath("a.txt");
        WorkspacePatchSecurityBinding.DeleteResources(path).ShouldBe([FileSecurityBinding.Resource(path)]);
    }

    [Fact]
    public void DeleteFingerprint_WhenEvidenceChanges_ChangesFingerprint()
    {
        var path = new FileSystemPath("a.txt");
        var expected = new ContentHash("sha256:old");
        var baseline = WorkspacePatchSecurityBinding.DeleteFingerprint(path, expected);
        WorkspacePatchSecurityBinding.DeleteFingerprint(new FileSystemPath("b.txt"), expected).ShouldNotBe(baseline);
        WorkspacePatchSecurityBinding.DeleteFingerprint(path, new ContentHash("sha256:other")).ShouldNotBe(baseline);
    }

    [Fact]
    public void MoveResources_WhenCalled_ReturnsSourceAndDestinationResources()
    {
        var source = new FileSystemPath("a.txt");
        var destination = new FileSystemPath("b.txt");
        WorkspacePatchSecurityBinding.MoveResources(source, destination).ShouldBe([FileSecurityBinding.Resource(source), FileSecurityBinding.Resource(destination)]);
    }

    [Fact]
    public void MoveFingerprint_WhenEvidenceChanges_ChangesFingerprint()
    {
        var source = new FileSystemPath("a.txt");
        var destination = new FileSystemPath("b.txt");
        var expected = new ContentHash("sha256:old");
        var baseline = WorkspacePatchSecurityBinding.MoveFingerprint(source, destination, expected);
        WorkspacePatchSecurityBinding.MoveFingerprint(new FileSystemPath("c.txt"), destination, expected).ShouldNotBe(baseline);
        WorkspacePatchSecurityBinding.MoveFingerprint(source, new FileSystemPath("d.txt"), expected).ShouldNotBe(baseline);
        WorkspacePatchSecurityBinding.MoveFingerprint(source, destination, new ContentHash("sha256:other")).ShouldNotBe(baseline);
    }
}
