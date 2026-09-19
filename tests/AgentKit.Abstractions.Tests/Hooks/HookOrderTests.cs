// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

public sealed class HookOrderTests
{
    [Fact]
    public void Constructor_WhenAnchorIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookOrder((HookOrderAnchor) 99));
        exception.ParamName.ShouldBe("anchor");
    }

    [Theory]
    [InlineData(HookOrderAnchor.Normal)]
    [InlineData(HookOrderAnchor.First)]
    [InlineData(HookOrderAnchor.Last)]
    public void Constructor_WhenAnchorIsDefined_RoundTripsThroughAnchor(HookOrderAnchor anchor) => new HookOrder(anchor).Anchor.ShouldBe(anchor);

    [Fact]
    public void Normal_IsSharedInstanceWithNormalAnchor() => HookOrder.Normal.Anchor.ShouldBe(HookOrderAnchor.Normal);

    [Fact]
    public void First_IsSharedInstanceWithFirstAnchor() => HookOrder.First.Anchor.ShouldBe(HookOrderAnchor.First);

    [Fact]
    public void Last_IsSharedInstanceWithLastAnchor() => HookOrder.Last.Anchor.ShouldBe(HookOrderAnchor.Last);

    [Fact]
    public void Equality_WhenAnchorsMatch_InstancesAreStructurallyEqual() => new HookOrder(HookOrderAnchor.First).ShouldBe(new HookOrder(HookOrderAnchor.First));
}
