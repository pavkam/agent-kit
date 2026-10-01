// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;


/// <summary>Verifies <see cref="ArtifactPin"/> validation.</summary>
public sealed class ArtifactPinTests
{
    private static readonly ArtifactReferenceCommitIntentId _intent = new(Guid.Parse("a0000000-0000-0000-0000-0000000000a1"));

    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var pin = new ArtifactPin(_intent, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1));
        pin.IntentId.ShouldBe(_intent);
        pin.PinnedAt.ShouldBe(DateTimeOffset.UnixEpoch);
        pin.HeldUntil.ShouldBe(DateTimeOffset.UnixEpoch.AddHours(1));
    }

    [Fact]
    public void Constructor_WhenIntentIsEmpty_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactPin(default, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1))).ParamName.ShouldBe("intentId");

    [Fact]
    public void Constructor_WhenHeldUntilIsNotAfterPinnedAt_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactPin(_intent, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("heldUntil");
}
