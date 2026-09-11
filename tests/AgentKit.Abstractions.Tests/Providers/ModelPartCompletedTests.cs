// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ModelPartCompleted behavior and contracts.</summary>
public sealed class ModelPartCompletedTests
{
    [Fact]
    public void ModelPartCompleted_Constructor_WhenPartNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ModelPartCompleted(new ModelRequestId(Guid.NewGuid()), 1, 0, null!));
        exception.ParamName.ShouldBe("part");
    }

    [Fact]
    public void ModelPartCompleted_Equality_WhenSameValues_InstancesAreEqual()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var part = new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty);
        new ModelPartCompleted(requestId, 1, 0, part).ShouldBe(new ModelPartCompleted(requestId, 1, 0, part));
    }
}
