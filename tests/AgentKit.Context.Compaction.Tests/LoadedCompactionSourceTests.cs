// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction.Tests;

public sealed class LoadedCompactionSourceTests
{
    [Fact]
    public void Constructor_WhenSnapshotIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new LoadedCompactionSource(null!, new SessionSequence(1), []));

        exception.ParamName.ShouldBe("snapshot");
    }

    [Fact]
    public void Constructor_WhenIneligibleTailParentsIsNull_ThrowsArgumentNullException()
    {
        var snapshot = CreateSnapshot();

        var exception = Should.Throw<ArgumentNullException>(
            () => new LoadedCompactionSource(snapshot, new SessionSequence(1), null!));

        exception.ParamName.ShouldBe("ineligibleTailParents");
    }

    [Fact]
    public void Constructor_WhenArgumentsValid_ExposesTheSuppliedEvidence()
    {
        var snapshot = CreateSnapshot();
        var branchTip = new SessionSequence(3);
        var parentId = new SessionEntryId(Guid.NewGuid());
        var ineligibleTailParents = ImmutableHashSet.Create(parentId);

        var source = new LoadedCompactionSource(snapshot, branchTip, ineligibleTailParents);

        source.Snapshot.ShouldBeSameAs(snapshot);
        source.BranchTip.ShouldBe(branchTip);
        source.IneligibleTailParents.ShouldBe(ineligibleTailParents);
    }

    [Fact]
    public void Equals_WhenSameEvidence_ReturnsTrueAndMatchingHashCode()
    {
        var snapshot = CreateSnapshot();
        var branchTip = new SessionSequence(3);
        ImmutableHashSet<SessionEntryId> ineligibleTailParents = [];
        var first = new LoadedCompactionSource(snapshot, branchTip, ineligibleTailParents);
        var second = new LoadedCompactionSource(snapshot, branchTip, ineligibleTailParents);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ToString().ShouldContain(nameof(LoadedCompactionSource.BranchTip));
    }

    [Fact]
    public void With_WhenCloned_ProducesAnEqualButDistinctInstance()
    {
        var snapshot = CreateSnapshot();
        var original = new LoadedCompactionSource(snapshot, new SessionSequence(1), []);

        var clone = original with { };

        clone.ShouldBe(original);
        ReferenceEquals(clone, original).ShouldBeFalse();
    }

    private static CompactionSourceSnapshot CreateSnapshot() => new(
        TestFactory.CompactionContext(),
        new BranchId(Guid.NewGuid()),
        new SessionVersion(1),
        new SessionSequence(1),
        []);
}
