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
}
