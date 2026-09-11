// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ModelPartStarted behavior and contracts.</summary>
public sealed class ModelPartStartedTests
{
    [Fact]
    public void ModelPartStarted_Equality_WhenSameValues_InstancesAreEqual()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        new ModelPartStarted(requestId, 1, 0).ShouldBe(new ModelPartStarted(requestId, 1, 0));
    }
}
