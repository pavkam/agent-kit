// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies DirectoryEnumerationRequest behavior and contracts.</summary>
public sealed class DirectoryEnumerationRequestTests
{
    [Fact]
    public void DirectoryEnumerationRequest_WhenMaximumEntriesIsZero_ThrowsBeforeGrantAssignment()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DirectoryEnumerationRequest(null, 0, null, SecurityTestData.Grant()));
        exception.ParamName.ShouldBe("maximumEntries");
    }

    [Fact]
    public void DirectoryEnumerationRequest_WhenGrantIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DirectoryEnumerationRequest(null, 1, null, null!));
        exception.ParamName.ShouldBe("grant");
    }
}
