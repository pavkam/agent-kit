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
}
