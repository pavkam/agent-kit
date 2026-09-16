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

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var incompatible = new DurableDecodeIncompatible<string>(new SchemaVersion("v1"), "reason");
        incompatible.RecordedVersion.ShouldBe(new SchemaVersion("v1"));
        incompatible.SafeReason.ShouldBe("reason");
    }

    [Fact]
    public void With_WhenSafeReasonIsWhitespace_ThrowsArgumentException()
    {
        var incompatible = new DurableDecodeIncompatible<string>(new SchemaVersion("v1"), "reason");
        Should.Throw<ArgumentException>(() => _ = incompatible with { SafeReason = " " }).ParamName.ShouldBe("SafeReason");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new DurableDecodeIncompatible<string>(new SchemaVersion("v1"), "reason");
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void With_WhenSafeReasonIsValid_UpdatesSafeReason()
    {
        var original = new DurableDecodeIncompatible<string>(new SchemaVersion("v1"), "reason");
        var updated = original with { SafeReason = "other reason" };
        updated.SafeReason.ShouldBe("other reason");
    }
}
