// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Compaction;

using AgentKit;

/// <summary>Verifies CompactionValidationRejected behavior and contracts.</summary>
public sealed class CompactionValidationRejectedTests
{
    [Fact]
    public void CompactionValidationRejected_Constructor_WhenIssuesDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new CompactionValidationRejected(default));
        exception.ParamName.ShouldBe("issues");
    }

    [Fact]
    public void CompactionValidationRejected_Constructor_WhenIssuesEmpty_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => new CompactionValidationRejected([]));
    [Fact]
    public void CompactionValidationRejected_Equality_WhenSameValues_InstancesAreEqual() => new CompactionValidationRejected([Issue()]).ShouldBe(new CompactionValidationRejected([Issue()]));
    [Fact]
    public void CompactionValidationRejected_Equality_WhenDifferentIssues_InstancesAreNotEqual()
    {
        var other = new CompactionValidationIssue(CompactionValidationIssueKind.NonReducing, "other", []);
        new CompactionValidationRejected([Issue()]).ShouldNotBe(new CompactionValidationRejected([other]));
    }

    private static CompactionValidationIssue Issue() => new(CompactionValidationIssueKind.InvalidStructure, "bad", [new SessionEntryId(Guid.Parse("77777777-7777-7777-7777-777777777777"))]);
}
