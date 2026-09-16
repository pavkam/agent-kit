// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies DurableDecoded behavior and contracts.</summary>
public sealed class DurableDecodedTests
{
    [Fact]
    public void DurableDecoded_Constructor_PreservesState() => new DurableDecoded<string>("state").State.ShouldBe("state");

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new DurableDecoded<string>("state");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
