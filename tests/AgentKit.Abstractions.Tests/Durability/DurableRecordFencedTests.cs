// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies DurableRecordFenced behavior and contracts.</summary>
public sealed class DurableRecordFencedTests
{
    [Fact]
    public void DurableRecordFenced_Constructor_WhenCurrentTokenIsNewer_Succeeds()
    {
        var fenced = new DurableRecordFenced(new FencingToken(1), new FencingToken(2));
        fenced.PresentedToken.ShouldBe(new FencingToken(1));
        fenced.CurrentToken.ShouldBe(new FencingToken(2));
    }

    [Fact]
    public void DurableRecordFenced_Constructor_WhenCurrentTokenEqualsPresented_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DurableRecordFenced(new FencingToken(2), new FencingToken(2)));
        exception.ParamName.ShouldBe("currentToken");
    }

    [Fact]
    public void DurableRecordFenced_Constructor_WhenCurrentTokenIsOlder_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DurableRecordFenced(new FencingToken(5), new FencingToken(4)));
        exception.ParamName.ShouldBe("currentToken");
    }

    [Fact]
    public void DurableRecordFenced_Constructor_WhenPresentedTokenIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DurableRecordFenced(default, new FencingToken(1)));
        exception.ParamName.ShouldBe("presentedToken");
    }
}
