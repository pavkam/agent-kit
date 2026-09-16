// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies AuthenticationEvidence behavior and contracts.</summary>
public sealed class AuthenticationEvidenceTests
{
    [Fact]
    public void AuthenticationEvidence_WhenExpiryDoesNotFollowAuthentication_ThrowsBeforeConstruction()
    {
        var authenticatedAt = DateTimeOffset.UnixEpoch;
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new AuthenticationEvidence(new AuthenticationEvidenceId("evidence"), new IdentityIssuerId("issuer"), "mfa", authenticatedAt, authenticatedAt, new AuthenticationEvidenceFingerprint(new ContentHash("hash"))));
        exception.ParamName.ShouldBe("expiresAt");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new AuthenticationEvidence(new AuthenticationEvidenceId("evidence"), new IdentityIssuerId("issuer"), "mfa", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1), new AuthenticationEvidenceFingerprint(new ContentHash("hash")));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
