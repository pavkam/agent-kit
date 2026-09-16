// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies IdentityNormalizationRequest behavior and contracts.</summary>
public sealed class IdentityNormalizationRequestTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var assertion = Assertion();
        var candidate = Candidate();
        var request = new IdentityNormalizationRequest(assertion, candidate);
        request.Assertion.ShouldBe(assertion);
        request.Candidate.ShouldBe(candidate);
    }

    [Fact]
    public void Constructor_WhenAssertionIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new IdentityNormalizationRequest(null!, Candidate()));
        exception.ParamName.ShouldBe("assertion");
    }

    [Fact]
    public void Constructor_WhenCandidateIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new IdentityNormalizationRequest(Assertion(), null!));
        exception.ParamName.ShouldBe("candidate");
    }

    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var assertion = Assertion();
        var candidate = Candidate();
        var first = new IdentityNormalizationRequest(assertion, candidate);
        var second = new IdentityNormalizationRequest(assertion, candidate);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new IdentityNormalizationRequest(Assertion(), Candidate());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ExecutionIdentity Candidate() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
    private static AuthenticationEvidence Evidence() => new(new AuthenticationEvidenceId("evidence"), new IdentityIssuerId("issuer"), "mfa", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1), new AuthenticationEvidenceFingerprint(new ContentHash("safe-hash")));
    private static IdentityAssertion Assertion() => new(new IdentityIssuerId("issuer"), "external-subject", [], Evidence());
}
