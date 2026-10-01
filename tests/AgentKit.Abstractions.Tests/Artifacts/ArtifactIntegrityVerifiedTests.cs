// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;


/// <summary>Verifies <see cref="ArtifactIntegrityVerified"/> validation.</summary>
public sealed class ArtifactIntegrityVerifiedTests
{
    [Fact]
    public void Constructor_WhenCalledWithHash_RetainsHash()
    {
        var verified = new ArtifactIntegrityVerified(new ContentHash("sha256:abc"));
        verified.ContentHash.ShouldBe(new ContentHash("sha256:abc"));
        _ = verified.ShouldBeAssignableTo<ArtifactIntegrityResult>();
    }

    [Fact]
    public void Constructor_WhenHashIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ArtifactIntegrityVerified(default)).ParamName.ShouldBe("contentHash");
}
