// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Compaction;

using AgentKit;

/// <summary>Verifies CompactionValidationIssue behavior and contracts.</summary>
public sealed class CompactionValidationIssueTests
{
    [Fact]
    public void CompactionValidationIssue_Constructor_WhenSourceEntryIdsDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new CompactionValidationIssue(CompactionValidationIssueKind.InvalidStructure, "bad", default));
        exception.ParamName.ShouldBe("sourceEntryIds");
    }

    [Fact]
    public void CompactionValidationIssue_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = Issue();
        var second = Issue();
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Issue();
        var copy = original with { };
        copy.ShouldBe(original);
    }
    [Fact]
    public void CompactionValidationIssue_Equality_WhenDifferentSourceEntryIds_InstancesAreNotEqual()
    {
        var id = new SessionEntryId(Guid.NewGuid());
        new CompactionValidationIssue(CompactionValidationIssueKind.InvalidStructure, "bad", [id]).ShouldNotBe(new CompactionValidationIssue(CompactionValidationIssueKind.InvalidStructure, "bad", [new SessionEntryId(Guid.NewGuid())]));
    }

    private static CompactionValidationIssue Issue() => new(CompactionValidationIssueKind.InvalidStructure, "bad", [new SessionEntryId(Guid.Parse("77777777-7777-7777-7777-777777777777"))]);
}
