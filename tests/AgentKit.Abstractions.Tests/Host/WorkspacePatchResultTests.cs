// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;



/// <summary>Verifies WorkspacePatchResult behavior and contracts.</summary>
public sealed class WorkspacePatchResultTests
{
    [Fact]
    public void WorkspacePatchResult_WhenEquivalentEntryArraysDifferByInstance_IsStructurallyEqual()
    {
        var path = new FileSystemPath("a.txt");
        var left = new WorkspacePatchResult(WorkspacePatchStatus.AtomicCommitted, [new WorkspacePatchEntryResult(0, WorkspacePatchEntryKind.Create, WorkspacePatchEntryStatus.Committed, path, null, new ContentHash("sha256:new"), null)], null);
        var right = new WorkspacePatchResult(WorkspacePatchStatus.AtomicCommitted, [new WorkspacePatchEntryResult(0, WorkspacePatchEntryKind.Create, WorkspacePatchEntryStatus.Committed, path, null, new ContentHash("sha256:new"), null)], null);
        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }
}
