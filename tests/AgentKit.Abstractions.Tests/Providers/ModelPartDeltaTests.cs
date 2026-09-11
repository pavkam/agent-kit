// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ModelPartDelta behavior and contracts.</summary>
public sealed class ModelPartDeltaTests
{
    [Fact]
    public void ModelPartDelta_Constructor_WhenDeltaNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ModelPartDelta(new ModelRequestId(Guid.NewGuid()), 1, 0, null!));
        exception.ParamName.ShouldBe("delta");
    }

    [Fact]
    public void ModelPartDelta_Equality_WhenSameValues_InstancesAreEqual()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var delta = new TextContentDelta("chunk");
        new ModelPartDelta(requestId, 1, 0, delta).ShouldBe(new ModelPartDelta(requestId, 1, 0, delta));
    }
}
