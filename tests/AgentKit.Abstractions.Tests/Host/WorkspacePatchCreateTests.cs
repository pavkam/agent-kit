// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;



/// <summary>Verifies WorkspacePatchCreate behavior and contracts.</summary>
public sealed class WorkspacePatchCreateTests
{
    [Fact]
    public void WorkspacePatchCreate_WhenContentIsDefault_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new WorkspacePatchCreate(new WorkspaceMutationId(Guid.NewGuid()), new FileSystemPath("a.txt"), default, SecurityTestData.Grant()));
        exception.ParamName.ShouldBe("content");
    }
}
