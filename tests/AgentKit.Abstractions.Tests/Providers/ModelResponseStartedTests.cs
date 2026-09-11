// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ModelResponseStarted behavior and contracts.</summary>
public sealed class ModelResponseStartedTests
{
    [Fact]
    public void ModelResponseStarted_Equality_WhenSameValues_InstancesAreEqual()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        new ModelResponseStarted(requestId, 1).ShouldBe(new ModelResponseStarted(requestId, 1));
    }
}
