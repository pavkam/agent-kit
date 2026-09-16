// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

/// <summary>Verifies WorkspacePatchEntryResult behavior and contracts.</summary>
public sealed class WorkspacePatchEntryResultTests
{
    [Fact]
    public void Constructor_WhenIndexIsNegative_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Result(index: -1)).ParamName.ShouldBe("index");

    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Result(kind: (WorkspacePatchEntryKind) 99)).ParamName.ShouldBe("kind");

    [Fact]
    public void Constructor_WhenStatusIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Result(status: (WorkspacePatchEntryStatus) 99)).ParamName.ShouldBe("status");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var result = Result();
        result.Index.ShouldBe(0);
        result.Kind.ShouldBe(WorkspacePatchEntryKind.Create);
        result.Status.ShouldBe(WorkspacePatchEntryStatus.Committed);
        result.SourcePath.ShouldBe(new FileSystemPath("a.txt"));
        result.DestinationPath.ShouldBeNull();
        result.ContentFingerprint.ShouldBe(new ContentHash("sha256:new"));
        result.SafeMessage.ShouldBeNull();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Result();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static WorkspacePatchEntryResult Result(
        int index = 0,
        WorkspacePatchEntryKind kind = WorkspacePatchEntryKind.Create,
        WorkspacePatchEntryStatus status = WorkspacePatchEntryStatus.Committed) =>
        new(index, kind, status, new FileSystemPath("a.txt"), null, new ContentHash("sha256:new"), null);
}
