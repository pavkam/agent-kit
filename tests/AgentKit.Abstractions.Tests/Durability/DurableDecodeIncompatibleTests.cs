// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies DurableDecodeIncompatible behavior and contracts.</summary>
public sealed class DurableDecodeIncompatibleTests
{
    [Fact]
    public void DurableDecodeIncompatible_Constructor_WhenReasonIsWhitespace_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new DurableDecodeIncompatible<string>(new SchemaVersion("v1"), "  "));
        exception.ParamName.ShouldBe("safeReason");
    }
}
