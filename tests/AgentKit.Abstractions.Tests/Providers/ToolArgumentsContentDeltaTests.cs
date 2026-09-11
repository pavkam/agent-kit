// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ToolArgumentsContentDelta behavior and contracts.</summary>
public sealed class ToolArgumentsContentDeltaTests
{
    [Fact]
    public void ToolArgumentsContentDelta_Constructor_WhenFragmentNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolArgumentsContentDelta(new ToolCallId(Guid.NewGuid()), null!));
        exception.ParamName.ShouldBe("jsonFragment");
    }

    [Fact]
    public void ToolArgumentsContentDelta_Constructor_WhenValid_RoundTripsProperties()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var delta = new ToolArgumentsContentDelta(callId, "{}");
        delta.ToolCallId.ShouldBe(callId);
        delta.JsonFragment.ShouldBe("{}");
    }

    [Fact]
    public void ToolArgumentsContentDelta_Equality_WhenSameValues_InstancesAreEqual()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        new ToolArgumentsContentDelta(callId, "{}").ShouldBe(new ToolArgumentsContentDelta(callId, "{}"));
    }
}
