// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;



/// <summary>Verifies GlobResult behavior and contracts.</summary>
public sealed class GlobResultTests
{
    [Fact]
    public void GlobResult_WhenEquivalentSequencesAreSeparateInstances_IsStructurallyEqual()
    {
        var left = new GlobResult(GlobStatus.Success, [new FileSystemPath("a.cs"), new FileSystemPath("src/b.cs")], 3, true, null);
        var right = new GlobResult(GlobStatus.Success, [new FileSystemPath("a.cs"), new FileSystemPath("src/b.cs")], 3, true, null);
        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new GlobResult(GlobStatus.Success, [new FileSystemPath("a.cs")], 1, true, null);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
