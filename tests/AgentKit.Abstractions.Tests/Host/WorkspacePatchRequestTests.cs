// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;



/// <summary>Verifies WorkspacePatchRequest behavior and contracts.</summary>
public sealed class WorkspacePatchRequestTests
{
    [Fact]
    public void WorkspacePatchRequest_WhenEntriesContainNull_ThrowsExactParameter()
    {
        ImmutableArray<WorkspacePatchEntry> entries = [null!];
        var exception = Should.Throw<ArgumentException>(() => new WorkspacePatchRequest(entries));
        exception.ParamName.ShouldBe("entries");
    }

    [Fact]
    public void WorkspacePatchRequest_WhenArgumentsAreValid_RoundTripsProperties()
    {
        WorkspacePatchEntry entry = new WorkspacePatchCreate(new WorkspaceMutationId(Guid.NewGuid()), new FileSystemPath("a.txt"), [1], SecurityTestData.Grant());
        var request = new WorkspacePatchRequest([entry]);
        request.Entries.ShouldBe([entry]);
    }

    [Fact]
    public void WorkspacePatchRequest_With_WhenApplied_ProducesEqualCopy()
    {
        WorkspacePatchEntry entry = new WorkspacePatchCreate(new WorkspaceMutationId(Guid.NewGuid()), new FileSystemPath("a.txt"), [1], SecurityTestData.Grant());
        var original = new WorkspacePatchRequest([entry]);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
