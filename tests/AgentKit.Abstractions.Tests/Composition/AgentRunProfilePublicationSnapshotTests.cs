// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;



/// <summary>Verifies AgentRunProfilePublicationSnapshot behavior and contracts.</summary>
public sealed class AgentRunProfilePublicationSnapshotTests
{
    [Fact]
    public void AgentRunProfilePublicationSnapshot_WhenArrayIsInvalid_ThrowsWithParameterName()
    {
        var uninitialized = Should.Throw<ArgumentException>(() => new AgentRunProfilePublicationSnapshot(default));
        var containingNull = Should.Throw<ArgumentException>(() => new AgentRunProfilePublicationSnapshot([null!]));
        uninitialized.ParamName.ShouldBe("publications");
        containingNull.ParamName.ShouldBe("publications");
    }
}
