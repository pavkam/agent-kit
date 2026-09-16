// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies LeaseLost behavior and contracts.</summary>
public sealed class LeaseLostTests
{
    [Fact]
    public void LeaseLost_Constructor_WhenTokenOmitted_CurrentTokenIsNull() => new LeaseLost().CurrentToken.ShouldBeNull();
    [Fact]
    public void LeaseLost_Constructor_WhenTokenIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new LeaseLost(default(FencingToken)));
        exception.ParamName.ShouldBe("currentToken");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new LeaseLost();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
