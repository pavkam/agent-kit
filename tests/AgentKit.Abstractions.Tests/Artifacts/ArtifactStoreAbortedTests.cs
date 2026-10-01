// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;


/// <summary>Verifies <see cref="ArtifactStoreAborted"/> value semantics.</summary>
public sealed class ArtifactStoreAbortedTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_WhenCalled_RetainsAbsenceFlag(bool alreadyAbsent)
    {
        var aborted = new ArtifactStoreAborted(alreadyAbsent);
        aborted.AlreadyAbsent.ShouldBe(alreadyAbsent);
        _ = aborted.ShouldBeAssignableTo<ArtifactStoreAbortResult>();
        aborted.ShouldBe(new ArtifactStoreAborted(alreadyAbsent));
    }
}
