// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies IdentityAssertion behavior and contracts.</summary>
public sealed class IdentityAssertionTests
{
    [Fact]
    public void IdentityAssertion_WhenEvidenceIssuerDiffers_ThrowsArgumentException()
    {
        var evidence = Evidence("issuer-a");
        var exception = Should.Throw<ArgumentException>(() => new IdentityAssertion(new IdentityIssuerId("issuer-b"), "external-subject", [], evidence));
        exception.ParamName.ShouldBe("evidence");
    }

    [Fact]
    public void IdentityAssertion_WhenClaimsUseDifferentArrayInstances_HasStructuralEquality()
    {
        var evidence = Evidence("issuer");
        var first = new IdentityAssertion(new IdentityIssuerId("issuer"), "subject", [new ExternalIdentityClaim("group", "a", IdentityClaimValueKind.Text)], evidence);
        var second = new IdentityAssertion(new IdentityIssuerId("issuer"), "subject", [new ExternalIdentityClaim("group", "a", IdentityClaimValueKind.Text)], evidence);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new IdentityAssertion(new IdentityIssuerId("issuer"), "subject", [], Evidence("issuer"));
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static AuthenticationEvidence Evidence(string issuer) => new(new AuthenticationEvidenceId("evidence"), new IdentityIssuerId(issuer), "mfa", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1), new AuthenticationEvidenceFingerprint(new ContentHash("safe-hash")));
}
