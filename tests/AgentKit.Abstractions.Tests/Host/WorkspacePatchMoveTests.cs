// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;



/// <summary>Verifies WorkspacePatchMove behavior and contracts.</summary>
public sealed class WorkspacePatchMoveTests
{
    [Fact]
    public void WorkspacePatchMove_WhenDestinationEqualsSource_ThrowsExactParameter()
    {
        var path = new FileSystemPath("a.txt");
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new WorkspacePatchMove(new WorkspaceMutationId(Guid.NewGuid()), path, path, new ContentHash("sha256:old"), SecurityTestData.Grant()));
        exception.ParamName.ShouldBe("destinationPath");
    }
}
