// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

/// <summary>Verifies AtomicFileReplaceResult behavior and contracts.</summary>
public sealed class AtomicFileReplaceResultTests
{
    [Fact]
    public void Constructor_WhenStatusIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new AtomicFileReplaceResult((AtomicFileReplaceStatus) 99, null, 0, null)).ParamName.ShouldBe("status");

    [Fact]
    public void Constructor_WhenBytesIsNegative_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new AtomicFileReplaceResult(AtomicFileReplaceStatus.Committed, null, -1, null)).ParamName.ShouldBe("bytes");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var fingerprint = new ContentHash("sha256:new");
        var result = new AtomicFileReplaceResult(AtomicFileReplaceStatus.Committed, fingerprint, 3, "Committed.");
        result.Status.ShouldBe(AtomicFileReplaceStatus.Committed);
        result.ContentFingerprint.ShouldBe(fingerprint);
        result.Bytes.ShouldBe(3);
        result.SafeMessage.ShouldBe("Committed.");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new AtomicFileReplaceResult(AtomicFileReplaceStatus.Committed, new ContentHash("sha256:new"), 3, "Committed.");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
