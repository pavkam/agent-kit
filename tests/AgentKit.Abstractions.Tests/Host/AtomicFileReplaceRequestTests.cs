// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;



/// <summary>Verifies AtomicFileReplaceRequest behavior and contracts.</summary>
public sealed class AtomicFileReplaceRequestTests
{
    [Fact]
    public void AtomicFileReplaceRequest_WhenContentIsDefault_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new AtomicFileReplaceRequest(new WorkspaceMutationId(Guid.NewGuid()), new FileSystemPath("a.txt"), new ContentHash("sha256:old"), default, SecurityTestData.Grant()));
        exception.ParamName.ShouldBe("content");
    }
}
